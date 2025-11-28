using NUnit.Framework.Interfaces;
using UnityEngine;

public interface IInteractable
{
    string PromptText { get; }
    void Interact(PlayerInventory player); // will be called on E
}

public class InteractablePickUp : MonoBehaviour, IInteractable
{
    [SerializeField] private ObjectData item;
    [SerializeField] private string prompt = "Press E to pick up";

    public string PromptText => prompt + $" {item?.objectName}";

    public void Interact(PlayerInventory player)
    {
        if (item == null) return;
        if (player.AddItem(item))
        {
            Destroy(gameObject); // picked!
        }
    }
}
