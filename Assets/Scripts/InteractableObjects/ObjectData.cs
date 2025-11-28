using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Objects/ObjectData")]
public class ObjectData : ScriptableObject
{
    public string itemId = System.Guid.NewGuid().ToString();
    public string objectName = "Item";
    public Sprite icon;
    public GameObject equipPrefab;
}
