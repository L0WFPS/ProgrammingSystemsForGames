using UnityEngine;
using UnityEngine.UI;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactRange = 4.0f;
    [SerializeField] private LayerMask interactMask;

    [Header("UI")]
    [SerializeField] private Text promptText; // optional legacy Text
    [SerializeField] private GameObject promptRoot;

    [Header("Refs")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Throwing")]
    [SerializeField] private float throwForce = 8f;
    [SerializeField] private Transform throwOrigin;



    private IInteractable hovered;
    private IButton button;

    private void Update()
    {
        UpdateHover();
        HandleInteractKey();
        HandleHotbarKeys();
        HandleThrowKey();

    }

    void UpdateHover()
    {
        hovered = null;
        button = null;
        if (playerCamera == null) return;

        var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out var hit, interactRange, interactMask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Interactable") || hit.collider.gameObject.layer == LayerMask.NameToLayer("Craftable"))
            {
                Debug.Log("Interactable");
                hovered = hit.collider.GetComponentInParent<IInteractable>();

            }
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Button"))
            {
                Debug.Log("Button");
                button = hit.collider.GetComponentInParent<IButton>();
            }

        }

        // UI prompt
        if (promptRoot != null)
            promptRoot.SetActive(hovered != null);
        if (promptText != null)
            promptText.text = hovered != null ? hovered.PromptText : "";
    }

    void HandleInteractKey()
    {
        if (Input.GetKeyDown(KeyCode.E) && hovered != null)
        {
            hovered.Interact(inventory);
        }
        if (Input.GetKeyDown(KeyCode.E) && button != null)
        {
            button.PressButton(); 
        }

        
    }

    void HandleHotbarKeys()
    {
        // 1..9 -> slots 0..8
        if (Input.GetKeyDown(KeyCode.Alpha1)) inventory.ToggleSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) inventory.ToggleSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) inventory.ToggleSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) inventory.ToggleSlot(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) inventory.ToggleSlot(4);
        if (Input.GetKeyDown(KeyCode.Alpha6)) inventory.ToggleSlot(5);
        if (Input.GetKeyDown(KeyCode.Alpha7)) inventory.ToggleSlot(6);
        if (Input.GetKeyDown(KeyCode.Alpha8)) inventory.ToggleSlot(7);
        if (Input.GetKeyDown(KeyCode.Alpha9)) inventory.ToggleSlot(8);
    }

    void HandleThrowKey()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            Transform origin = throwOrigin;
            inventory.DropSelectedAndThrow(origin, throwForce);
        }
    }

}
