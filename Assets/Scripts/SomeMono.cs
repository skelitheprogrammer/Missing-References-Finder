using UnityEngine;

public class SomeMono : MonoBehaviour
{
    public SomeScriptableObject SomeSo;

    private void Awake()
    {
        Component[] components = gameObject.GetComponents<Component>();
        foreach (Component component in components)
        {
            Debug.Log(component.gameObject.name);
        }
    }
}