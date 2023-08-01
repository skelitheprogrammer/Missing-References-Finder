using UnityEngine.UIElements;

public static class UIToolkitStyleExtensions
{
    public static void SetDisplayState(this VisualElement visualElement, bool state)
    {
        visualElement.style.display = state ? DisplayStyle.Flex : DisplayStyle.None;
    }
}