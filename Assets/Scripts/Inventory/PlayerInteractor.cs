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
    [SerializeField] private GameObject recipeUI;

    [Header("Refs")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Throwing / Dropping")]
    [SerializeField] private float throwForce = 8f;
    [SerializeField] private Transform throwOrigin;
    [SerializeField] private Transform dropOrigin;

    private IInteractable hovered;
    private IButton button;
    private IDoor door;

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();

        // If you don't assign these in inspector, fall back to camera transform
        if (playerCamera != null)
        {
            if (throwOrigin == null) throwOrigin = playerCamera.transform;
            if (dropOrigin == null) dropOrigin = playerCamera.transform;
        }
        else
        {
            if (throwOrigin == null) throwOrigin = transform;
            if (dropOrigin == null) dropOrigin = transform;
        }
    }

    private void Update()
    {
        UpdateHover();
        HandleInteractKey();
        HandleHotbarKeys();
        HandleThrowAndDrop();
        UIToggle();
    }

    void UpdateHover()
    {
        hovered = null;
        button = null;
        door = null;

        if (playerCamera == null) return;

        var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out var hit, interactRange, interactMask, QueryTriggerInteraction.Collide))
        {
            int layer = hit.collider.gameObject.layer;

            if (layer == LayerMask.NameToLayer("Interactable") || layer == LayerMask.NameToLayer("Craftable"))
            {
                hovered = hit.collider.GetComponentInParent<IInteractable>();
            }
            else if (layer == LayerMask.NameToLayer("Button"))
            {
                button = hit.collider.GetComponentInParent<IButton>();
            }
            else if (layer == LayerMask.NameToLayer("Door"))
            {
                door = hit.collider.GetComponentInParent<IDoor>();
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
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (hovered != null)
            {
                hovered.Interact(inventory);
                return;
            }

            if (button != null)
            {
                button.PressButton();
                return;
            }

            if (door != null)
            {
                // ✅ Always open away from the player by passing our transform as opener.
                if (!door.IsOpen) door.OpenDoor(transform);
                else door.CloseDoor();
                return;
            }
        }
    }

    void HandleHotbarKeys()
    {
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

    void HandleThrowAndDrop()
    {
        if (inventory == null) return;

        // LEFT CLICK = THROW selected
        if (Input.GetMouseButtonDown(0))
        {
            Transform origin = throwOrigin != null ? throwOrigin : transform;
            inventory.ThrowSelected(origin, throwForce);
        }

        // G = DROP selected (no throw)
        if (Input.GetKeyDown(KeyCode.G))
        {
            Transform origin = dropOrigin != null ? dropOrigin : transform;
            inventory.DropSelected(origin);
        }
    }

    void UIToggle()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            recipeUI.SetActive(!recipeUI.activeSelf);
        }
    }
}
