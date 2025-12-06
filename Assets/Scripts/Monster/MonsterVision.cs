using UnityEngine;

public class MonsterVision : MonoBehaviour
{
    [Header("Vision Settings")]
    [Tooltip("Maximum distance the monster can see the player.")]
    public float viewDistance = 22f;

    [Tooltip("Horizontal field of view in degrees (wide so it can see while patrolling).")]
    public float viewAngle = 140f;

    [Header("What blocks vision (set to Walls in Inspector)")]
    public LayerMask obstructionMask; // SHOULD be just Walls (and optionally Ground if you want floors to block)

    private CapsuleCollider monsterCol;
    private CapsuleCollider playerCol;

    private void Awake()
    {
        // We assume the monster has a CapsuleCollider on its body child
        monsterCol = GetComponentInChildren<CapsuleCollider>();
    }

    public bool CanSee(Transform target)
    {
        if (target == null)
            return false;

        // Cache the player's capsule collider (on Body)
        if (playerCol == null)
            playerCol = target.GetComponentInChildren<CapsuleCollider>();

        if (monsterCol == null || playerCol == null)
            return false;

        // --- RAY ORIGIN ---
        // Start from slightly above + in front of the monster's chest
        // so we don't raycast inside its own collider or into the floor edge.
        Vector3 origin =
            monsterCol.bounds.center +
            Vector3.up * 0.15f +       // lift a bit
            transform.forward * 0.45f; // push forward out of chest

        // Aim at the center of the player's collider (good for standing / crouch)
        Vector3 targetPoint = playerCol.bounds.center;

        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance > viewDistance)
            return false;

        Vector3 dir = toTarget.normalized;

        // --- FOV CHECK ---
        float angle = Vector3.Angle(transform.forward, dir);
        if (angle > viewAngle * 0.5f)
            return false;

        // --- OBSTRUCTION CHECK ---
        // We cast ONLY against obstructionMask (walls, optionally ground).
        // If we hit something AND it's not part of the player, vision is blocked.
        if (Physics.Raycast(origin, dir, out RaycastHit hit, distance, obstructionMask))
        {
            if (!hit.collider.transform.IsChildOf(target))
            {
                // Hit a wall or something else before the player.
                return false;
            }
        }

        // No blocking walls → player is visible
        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (monsterCol == null) return;

        Vector3 origin =
            monsterCol.bounds.center +
            Vector3.up * 0.15f +
            transform.forward * 0.45f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, viewDistance);

        float half = viewAngle * 0.5f;
        Vector3 leftDir = Quaternion.Euler(0, -half, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, half, 0) * transform.forward;

        Gizmos.DrawLine(origin, origin + leftDir * viewDistance);
        Gizmos.DrawLine(origin, origin + rightDir * viewDistance);
    }
#endif
}
