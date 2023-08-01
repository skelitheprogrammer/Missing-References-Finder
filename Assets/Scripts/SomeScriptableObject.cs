using UnityEngine;

[CreateAssetMenu(menuName = "Create SomeScriptableObject", fileName = "SomeScriptableObject", order = 0)]
public class SomeScriptableObject : ScriptableObject
{
    public GameObject SomePrefab;
    public SomeMonoReferenceScript SomeMonoReferenceScript;
}