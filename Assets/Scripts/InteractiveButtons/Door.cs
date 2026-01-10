using System.Collections;
using UnityEngine;

public class Door : MonoBehaviour, IDoor
{
    [Header("References")]
    [SerializeField] private Transform hinge;

    [Header("Motion")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 180f;
    [SerializeField] private bool invertOpenDirection = false;

    [Header("Open Direction")]
    [Tooltip("If true, door opens away from the opener.")]
    [SerializeField] private bool openAwayFromOpener = true;

    [Header("Collision Safety")]
    [SerializeField] private bool ignoreOpenerCollisionWhileMoving = true;
    [SerializeField] private float ignoreCollisionTime = 0.6f;

    public bool IsOpen => isOpen;

    private bool isOpen;
    private bool isMoving;
    private float currentOpenSignedAngle;
    private Coroutine moveRoutine;
    private Collider doorCollider;

    private void Awake()
    {
        doorCollider = GetComponent<Collider>();

        if (hinge == null)
            Debug.LogWarning($"[{name}] Door has no hinge assigned.");
    }

    // ---------------- PUBLIC API ----------------

    public void ToggleDoor()
    {
        // Legacy support (no opener info)
        if (isMoving) return;

        if (isOpen) CloseDoor();
        else OpenDoor(null);
    }

    public void OpenDoor() => OpenDoor(null);

    public void OpenDoor(Transform opener)
    {
        if (isMoving || isOpen) return;

        float sign = 1f;

        // ?? Core logic: pick the swing direction that moves AWAY from opener
        if (openAwayFromOpener && opener != null && hinge != null)
        {
            const float testAngle = 5f;

            Vector3 openerPos = opener.position;
            openerPos.y = transform.position.y;

            Vector3 curPos = transform.position;

            Vector3 posPlus = RotatePointAroundPivotY(curPos, hinge.position, +testAngle);
            Vector3 posMinus = RotatePointAroundPivotY(curPos, hinge.position, -testAngle);

            float dPlus = (openerPos - posPlus).sqrMagnitude;
            float dMinus = (openerPos - posMinus).sqrMagnitude;

            sign = (dPlus > dMinus) ? 1f : -1f;
        }

        if (invertOpenDirection)
            sign *= -1f;

        currentOpenSignedAngle = openAngle * sign;

        if (ignoreOpenerCollisionWhileMoving && opener != null)
            StartCoroutine(TemporarilyIgnoreCollisionWith(opener));

        StartMove(opening: true);
    }

    public void CloseDoor()
    {
        if (isMoving || !isOpen) return;
        StartMove(opening: false);
    }

    // ---------------- INTERNAL ----------------

    private void StartMove(bool opening)
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(MoveDoor(opening));
    }

    private IEnumerator MoveDoor(bool opening)
    {
        isMoving = true;

        float targetAngle = opening ? currentOpenSignedAngle : -currentOpenSignedAngle;
        float rotated = 0f;

        while (Mathf.Abs(rotated) < Mathf.Abs(targetAngle) - 0.01f)
        {
            float step = openSpeed * Time.deltaTime;
            float remaining = Mathf.Abs(targetAngle) - Mathf.Abs(rotated);
            step = Mathf.Min(step, remaining);

            float signedStep = Mathf.Sign(targetAngle) * step;

            if (hinge != null)
                transform.RotateAround(hinge.position, Vector3.up, signedStep);
            else
                transform.Rotate(Vector3.up, signedStep, Space.World);

            rotated += signedStep;
            yield return null;
        }

        isOpen = opening;
        isMoving = false;
        moveRoutine = null;
    }

    private IEnumerator TemporarilyIgnoreCollisionWith(Transform opener)
    {
        if (doorCollider == null || opener == null)
            yield break;

        Collider[] openerColliders = opener.GetComponentsInChildren<Collider>(true);

        foreach (var c in openerColliders)
            Physics.IgnoreCollision(doorCollider, c, true);

        yield return new WaitForSeconds(ignoreCollisionTime);

        foreach (var c in openerColliders)
            Physics.IgnoreCollision(doorCollider, c, false);
    }

    private static Vector3 RotatePointAroundPivotY(Vector3 point, Vector3 pivot, float angleDeg)
    {
        Vector3 dir = point - pivot;
        Quaternion rot = Quaternion.AngleAxis(angleDeg, Vector3.up);
        dir = rot * dir;
        return pivot + dir;
    }
}
