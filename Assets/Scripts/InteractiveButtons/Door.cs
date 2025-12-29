using UnityEngine;

public interface IDoor
{
    void ToggleDoor();
}

public class Door : MonoBehaviour, IDoor
{
    public Transform hinge;            // RIGHT side hinge
    public float openAngle = 90f;
    public float openSpeed = 90f;       // degrees per second
    public bool openInverted = false;

    private bool isOpen = false;
    private float currentAngle = 0f;
    private float targetAngle = 0f;

    void Update()
    {
        if (Mathf.Approximately(currentAngle, targetAngle))
            return;

        float direction = Mathf.Sign(targetAngle - currentAngle);
        float step = openSpeed * Time.deltaTime * direction;

        // Prevent overshoot
        if (Mathf.Abs(step) > Mathf.Abs(targetAngle - currentAngle))
            step = targetAngle - currentAngle;

        Vector3 axis = Vector3.up;
        if (openInverted) axis = -axis;

        transform.RotateAround(
            hinge.position,
            axis,
            step
        );

        currentAngle += step;
    }

    public void ToggleDoor()
    {
        targetAngle = isOpen ? 0f : openAngle;
        isOpen = !isOpen;
    }
}
