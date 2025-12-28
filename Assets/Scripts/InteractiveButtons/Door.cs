using UnityEngine;

public interface IDoor
{
    void ToggleDoor();
}

public class Door : MonoBehaviour, IDoor
{
    public float openAngle = 90f;
    public float openSpeed = 4f;
    public bool openInverted = false;

    private bool isOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    void Start()
    {
        closedRotation = transform.rotation;

        float direction = openInverted ? -1f : 1f;
        openRotation = Quaternion.Euler(
            transform.eulerAngles + Vector3.up * openAngle * direction
        );
    }

    void Update()
    {
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * openSpeed
        );
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
    }
}
