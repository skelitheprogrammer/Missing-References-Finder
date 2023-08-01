using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class MissingReferecnceFinder : EditorWindow
{
    [SerializeField] private VisualTreeAsset _treeAsset;
    [SerializeField] private VisualTreeAsset _assetItemAsset;
    [SerializeField] private VisualTreeAsset _missingComponentItemAsset;

    private const string ASSETS_PATH = "Assets";
    private const string WINDOW_NAME = "Missing Reference Finder";

    private static readonly List<string> _ignoreKeyWords = new List<string>
    {
        "Base",
        "objectReference",
        "m_CorrespondingSourceObject",
        "m_PrefabInstance",
        "m_PrefabAsset",
        "m_GameObject",
        "m_Icon",
        "m_Script",
        "m_Father"
    };

    private static readonly List<string> _ignorePatterns = new List<string>
    {
        @"link\.xml$",
        @"\.csv$",
        @"\.png$",
        @"\.md$",
        @"\.json$",
        @"\.xml$",
        @"\.uxml$",
        @"\.uss$",
        @"\.txt$",
        @"\.cs$",
        @"\.asmdef$",
    };
    
    private const string CONTENT_SCROLLVIEW = "MissingAssetsScrollView";
    private ScrollView _missingAssetItemsScrollView;

    private const string RESULT_SECTION = "ResultSection";

    private const string START_SEARCH_BUTTON = "StartSearchButton";
    private Button _startSearchButton;
    
    private const string TOTAL_SEARCH_TIME_LABEL = "SearchTimeLabel";
    private Label _totalSearchTimeLabel;
    private string _defaultTotalSearchTimeText;

    private const string FOUND_MISSING_ASSETS_COUNT = "MissingAssetComponentsLabel";
    private Label _missingAssetsCountLabel;
    private string _defaultMissingAssetsCountText;

    private const string ASSET_ITEM_SELECT_ASSET_BUTTON = "AssetNameSelectButton";
    private const string ASSET_ITEM_PATH_LABEL = "AssetPathLabel";
    private const string ASSET_ITEM_FOLDOUT = "MissingComponentsFoldout";

    private const string MISSING_COMPONENT_ITEM_NAME_LABEL = "MissingComponentNameLabel";
    private const string MISSING_COMPONENT_ITEM_FOLDOUT = "MissingComponentFoldout";


    [MenuItem("Tools/" + WINDOW_NAME)]
    public static void ShowWindow()
    {
        MissingReferecnceFinder wnd = GetWindow<MissingReferecnceFinder>();
        wnd.titleContent = new GUIContent(WINDOW_NAME);
    }

    private void CreateGUI()
    {
        VisualElement root = rootVisualElement;

        VisualTreeAsset visualTree = _treeAsset;
        VisualElement labelFromUxml = visualTree.Instantiate();
        root.Add(labelFromUxml);

        _missingAssetItemsScrollView = root.Q<ScrollView>(CONTENT_SCROLLVIEW);

        VisualElement resultSection = root.Q<VisualElement>(RESULT_SECTION);
        _startSearchButton = resultSection.Q<Button>(START_SEARCH_BUTTON);
        _startSearchButton.clickable.clicked += StartSearchHandle;

        _totalSearchTimeLabel = resultSection.Q<Label>(TOTAL_SEARCH_TIME_LABEL);

        _defaultTotalSearchTimeText = _totalSearchTimeLabel.text;
        
        _missingAssetsCountLabel = resultSection.Q<Label>(FOUND_MISSING_ASSETS_COUNT);
        _defaultMissingAssetsCountText = _missingAssetsCountLabel.text;

        ResetView();
    }

    private void OnDestroy()
    {
        ResetView();
    }

    private void ResetView()
    {
        _startSearchButton.SetEnabled(true);

        _missingAssetItemsScrollView.contentContainer.Clear();

        _missingAssetsCountLabel.text = _defaultMissingAssetsCountText;
        _missingAssetsCountLabel.SetDisplayState(false);

        _totalSearchTimeLabel.text = _defaultTotalSearchTimeText;
        _totalSearchTimeLabel.SetDisplayState(false);
        
    }

    private void SetTotalMissingReferencesText(int count)
    {
        _missingAssetsCountLabel.text = $"{_defaultMissingAssetsCountText}: {count}";
    }

    private void SetTotalSearchTimeText(double time)
    {
        _totalSearchTimeLabel.text = $"{_defaultTotalSearchTimeText}: {time}";
    }
    
    //can't convert it to async due Unity issues with async workflow
    private void StartSearchHandle()
    {
        ResetView();

        _startSearchButton.SetEnabled(false);

        double startTime = Time.realtimeSinceStartupAsDouble;

        AssetItemData[] foundItemDatas = FindAllAssets();
        List<AssetItem> missingAssetItems = new List<AssetItem>();

        for (int index = 0; index < foundItemDatas.Length; index++)
        {
            AssetItemData foundItemData = foundItemDatas[index];
            
            if (foundItemData.Obj is GameObject gameObject)
            {
                if (TryFindMissingReferencesOnGameObject(gameObject, foundItemData, out AssetItem gameObjectAssetItem))
                {
                    missingAssetItems.Add(gameObjectAssetItem);
                }

                if (gameObject.transform.childCount > 0)
                {
                    foreach (Transform child in gameObject.transform)
                    {
                        if (TryFindMissingReferencesOnGameObject(child.gameObject, foundItemData, out AssetItem childAssetItem))
                        {
                            missingAssetItems.Add(childAssetItem);
                        }
                    }
                }

                continue;
            }

            if (TryFindMissingReferences(foundItemData, out AssetItem assetItem))
            {
                missingAssetItems.Add(assetItem);
            }
        }

        double endTime = Time.realtimeSinceStartupAsDouble;

        _totalSearchTimeLabel.SetDisplayState(true);
        SetTotalSearchTimeText(endTime - startTime);

        _missingAssetsCountLabel.SetDisplayState(true);
        SetTotalMissingReferencesText(missingAssetItems.Count);
        
        _startSearchButton.SetEnabled(true);

        PopulateScrollView(missingAssetItems);
    }

    private void PopulateScrollView(IEnumerable<AssetItem> assetItems)
    {
        foreach (AssetItem assetItem in assetItems)
        {
            CreateAssetItem(assetItem, _missingAssetItemsScrollView, out Foldout assetItemFoldout);

            foreach (MissingComponentReferenceItem missingComponentReferenceItem in assetItem.MissingComponentReferenceItems)
            {
                CreateMissingReferenceItem(missingComponentReferenceItem, assetItemFoldout, out Foldout missingComponentFoldout);

                foreach (MissingFieldReferenceItem missingPropertyReferenceItem in missingComponentReferenceItem.MissingPropertyReferenceItems)
                {
                    CreateMissingPropertyObjectField(missingPropertyReferenceItem, missingComponentFoldout);
                }
            }
        }
    }

    private void CreateAssetItem(AssetItem assetItem, ScrollView scrollView, out Foldout foldout)
    {
        VisualElement element = _assetItemAsset.CloneTree();
        Button button = element.Q<Button>(ASSET_ITEM_SELECT_ASSET_BUTTON);
        button.text = assetItem.ItemData.AssetName;
        button.clickable.clicked += HandleNavigateSelectedAssetItem;
        element.Q<TextField>(ASSET_ITEM_PATH_LABEL).value = assetItem.ItemData.AssetPath;
        foldout = element.Q<Foldout>(ASSET_ITEM_FOLDOUT);

        scrollView.Add(element);

        void HandleNavigateSelectedAssetItem()
        {
            Selection.activeObject = assetItem.ItemData.Obj;
        }
    }

    private void CreateMissingReferenceItem(MissingComponentReferenceItem missingComponentReferenceItem, Foldout parentFoldout, out Foldout foldout)
    {
        TemplateContainer element = _missingComponentItemAsset.CloneTree();
        element.Q<Label>(MISSING_COMPONENT_ITEM_NAME_LABEL).text = missingComponentReferenceItem.ComponentName;
        foldout = element.Q<Foldout>(MISSING_COMPONENT_ITEM_FOLDOUT);

        parentFoldout.Add(element.contentContainer);
    }

    private void CreateMissingPropertyObjectField(MissingFieldReferenceItem missingFieldReferenceItem, Foldout parentFoldout)
    {
        //Could not find solution to extract matching type with missing object from serializedProperty
        ObjectField missingPropertyObjectField = new ObjectField
        {
            label = missingFieldReferenceItem.FieldName,
            allowSceneObjects = true,
            objectType = typeof(Object),
        };
        
        missingPropertyObjectField.Bind(missingFieldReferenceItem.SerializedObject);
        missingPropertyObjectField.bindingPath = missingFieldReferenceItem.FieldPath;

        parentFoldout.Add(missingPropertyObjectField);
    }

    private static AssetItemData[] FindAllAssets()
    {
        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths()
            .Where(path => path.StartsWith(ASSETS_PATH + "/"))
            .Where(IsPatternMatch)
            .ToArray();

        AssetItemData[] itemDatas = new AssetItemData[allAssetPaths.Length];

        for (int i = 0; i < allAssetPaths.Length; i++)
        {
            string allAssetPath = allAssetPaths[i];
            Object obj = AssetDatabase.LoadMainAssetAtPath(allAssetPath);

            itemDatas[i] = new AssetItemData()
            {
                AssetName = obj.name,
                AssetPath = allAssetPath,
                Obj = obj
            };
        }

        static bool IsPatternMatch(string path)
        {
            bool isMatch = _ignorePatterns.All(pattern => string.IsNullOrEmpty(pattern) || !Regex.Match(path, pattern).Success);

            return isMatch;
        }

        return itemDatas;
    }

    private static bool TryFindMissingReferencesOnGameObject(GameObject gameObject, AssetItemData assetItemData, out AssetItem assetItem)
    {
        assetItem = null;
        Component[] components = gameObject.GetComponents<Component>();

        if (components.Length == 0)
        {
            return false;
        }

        List<MissingComponentReferenceItem> missingComponentReferenceItems = new List<MissingComponentReferenceItem>();

        foreach (Component component in components)
        {
            if (TryFindComponentMissingReferences(component, component.GetType().Name, out MissingComponentReferenceItem missingComponentReferenceItem))
            {
                missingComponentReferenceItems.Add(missingComponentReferenceItem);
            }
        }

        if (missingComponentReferenceItems.Count == 0)
        {
            return false;
        }

        assetItem = new AssetItem
        {
            ItemData = assetItemData,
            MissingComponentReferenceItems = missingComponentReferenceItems.ToArray()
        };
        
        return true;
    }

    private static bool TryFindMissingReferences(AssetItemData assetItemData, out AssetItem assetItem)
    {
        assetItem = null;

        if (!TryFindComponentMissingReferences(assetItemData.Obj, assetItemData.AssetName, out MissingComponentReferenceItem test))
        {
            return false;
        }

        assetItem = new AssetItem
        {
            ItemData = assetItemData,
            MissingComponentReferenceItems = new[] {test}
        };

        return true;
    }

    private static bool TryFindComponentMissingReferences(Object obj, string displayName, out MissingComponentReferenceItem missingComponentReferenceItem)
    {
        missingComponentReferenceItem = null;
        SerializedObject serializedObject = new SerializedObject(obj);

        MissingFieldReferenceItem[] missingPropertyReferenceItems = FindMissingPropertyValues(serializedObject);

        if (missingPropertyReferenceItems.Length == 0)
        {
            return false;
        }

        missingComponentReferenceItem = new MissingComponentReferenceItem
        {
            ComponentName = displayName,
            MissingPropertyReferenceItems = missingPropertyReferenceItems
        };

        return true;
    }

    private static MissingFieldReferenceItem[] FindMissingPropertyValues(SerializedObject serializedObject)
    {
        List<MissingFieldReferenceItem> missingPropertyReferenceItems = new List<MissingFieldReferenceItem>();
        
        SerializedProperty iterator = serializedObject.GetIterator();
        
        do
        {
            bool isNotObjectReference = iterator.propertyType != SerializedPropertyType.ObjectReference;

            if (isNotObjectReference)
            {
                continue;
            }
            
            bool isInvalidName = false;

            for (int index = 0; index < _ignoreKeyWords.Count; index++)
            {
                string ignoreKeyWord = _ignoreKeyWords[index];
                if (!iterator.name.Contains(ignoreKeyWord))
                {
                    continue;
                }

                isInvalidName = true;
                break;
            }

            if (isInvalidName)
            {
                continue;
            }

            bool isNull = iterator.objectReferenceValue == null;
            bool isMissing = isNull && iterator.objectReferenceInstanceIDValue != 0;

            if (!isMissing)
            {
                continue;
            }
            
            missingPropertyReferenceItems.Add(new MissingFieldReferenceItem
            {
                FieldName = iterator.displayName,
                FieldPath = iterator.propertyPath,
                SerializedObject = serializedObject,
            });
        } while (iterator.NextVisible(true));

        return missingPropertyReferenceItems.ToArray();
    }
    
    private class AssetItem
    {
        public AssetItemData ItemData;
        public IEnumerable<MissingComponentReferenceItem> MissingComponentReferenceItems;
    }

    private class AssetItemData
    {
        public string AssetName;
        public string AssetPath;
        public Object Obj;
    }

    private class MissingComponentReferenceItem
    {
        public string ComponentName;
        public IEnumerable<MissingFieldReferenceItem> MissingPropertyReferenceItems;
    }

    private class MissingFieldReferenceItem
    {
        public string FieldName;
        public string FieldPath;
        public SerializedObject SerializedObject;
    }
}