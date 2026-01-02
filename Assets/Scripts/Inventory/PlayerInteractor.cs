using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactRange = 4f;
    [SerializeField] private LayerMask interactMask;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask))
        {
            // Works if Door is on the root OR on a parent
            IDoor door = hit.collider.GetComponentInParent<IDoor>();
            if (door != null)
            {
                door.ToggleDoor();
            }
        }
    }
}
