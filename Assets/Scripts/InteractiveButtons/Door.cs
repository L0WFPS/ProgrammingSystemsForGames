using System.Collections;
using UnityEngine;

public class Door : MonoBehaviour, IDoor
{
    [Header("References")]
    [SerializeField] private Transform hinge;

    [Header("Motion")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 180f; // degrees per second
    [SerializeField] private bool invertOpenDirection = false;

    [Header("Safety / Quality")]
    [Tooltip("If true, door chooses an open direction that swings away from the opener (monster/player) to reduce trapping.")]
    [SerializeField] private bool openAwayFromOpener = true;

    [Tooltip("If true, while the door is swinging we temporarily ignore collision between the door and the opener.")]
    [SerializeField] private bool ignoreOpenerCollisionWhileMoving = true;

    [Tooltip("How long to ignore collision while door swings (seconds).")]
    [SerializeField] private float ignoreCollisionTime = 0.6f;

    public bool IsOpen => isOpen;

    private bool isOpen;
    private bool isMoving;
    private Quaternion closedRotation;
    private float currentOpenSignedAngle; // keeps track of which direction we opened this time
    private Coroutine moveRoutine;

    private Collider doorCollider;

    private void Awake()
    {
        doorCollider = GetComponent<Collider>();
        closedRotation = transform.rotation;

        if (hinge == null)
            Debug.LogWarning($"[{name}] Door has no hinge reference set.");
    }

    public void ToggleDoor()
    {
        if (isMoving) return;

        if (isOpen) CloseDoor();
        else OpenDoor(null);
    }

    public void OpenDoor() => OpenDoor(null);

    public void OpenDoor(Transform opener)
    {
        if (isMoving) return;
        if (isOpen) return;

        float sign = 1f;

        // Pick a direction that swings away from the opener (helps monster not get pinned)
        if (openAwayFromOpener && opener != null && hinge != null)
        {
            // If opener is on the "front" side of the door, open one way; if behind, open the other.
            Vector3 toOpener = opener.position - transform.position;
            float side = Vector3.Dot(transform.forward, toOpener);

            // If opener is in front of the door, open away from them
            // (flip sign depending on your door orientation)
            sign = (side >= 0f) ? 1f : -1f;
        }

        if (invertOpenDirection) sign *= -1f;

        currentOpenSignedAngle = openAngle * sign;

        if (ignoreOpenerCollisionWhileMoving && opener != null)
            StartCoroutine(TemporarilyIgnoreCollisionWith(opener, ignoreCollisionTime));

        StartMove(true);
    }

    public void CloseDoor()
    {
        if (isMoving) return;
        if (!isOpen) return;

        StartMove(false);
    }

    private void StartMove(bool opening)
    {
        if (hinge == null)
        {
            // Fallback: rotate in place (won’t pivot correctly, but avoids hard break)
            Debug.LogWarning($"[{name}] Door missing hinge, rotating without pivot.");
        }

        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveDoor(opening));
    }

    private IEnumerator MoveDoor(bool opening)
    {
        isMoving = true;

        // We rotate around the hinge in world space
        float targetAngle = opening ? currentOpenSignedAngle : -currentOpenSignedAngle;
        float moved = 0f;

        while (Mathf.Abs(moved) < Mathf.Abs(targetAngle) - 0.01f)
        {
            float step = openSpeed * Time.deltaTime;
            float remaining = Mathf.Abs(targetAngle) - Mathf.Abs(moved);
            step = Mathf.Min(step, remaining);

            float signedStep = Mathf.Sign(targetAngle) * step;

            if (hinge != null)
                transform.RotateAround(hinge.position, Vector3.up, signedStep);
            else
                transform.Rotate(Vector3.up, signedStep, Space.World);

            moved += signedStep;
            yield return null;
        }

        isOpen = opening;
        isMoving = false;

        // Snap exact closed rotation when closing (prevents drift after many opens/closes)
        if (!opening)
        {
            transform.rotation = closedRotation;
        }

        moveRoutine = null;
    }

    private IEnumerator TemporarilyIgnoreCollisionWith(Transform opener, float seconds)
    {
        if (doorCollider == null) yield break;

        Collider[] openerCols = opener.GetComponentsInChildren<Collider>();
        if (openerCols == null || openerCols.Length == 0) yield break;

        foreach (var c in openerCols)
        {
            if (c != null)
                Physics.IgnoreCollision(doorCollider, c, true);
        }

        yield return new WaitForSeconds(seconds);

        foreach (var c in openerCols)
        {
            if (c != null)
                Physics.IgnoreCollision(doorCollider, c, false);
        }
    }
}
