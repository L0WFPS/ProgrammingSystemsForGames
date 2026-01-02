using System;
using System.Collections.Generic;
using UnityEngine;

public class MonsterVision : MonoBehaviour
{
    [Header("Vision")]
    public float viewDistance = 22f;
    [Range(1f, 179f)] public float viewAngle = 140f;

    [Header("Layers")]
    public LayerMask targetMask;       // Player
    public LayerMask obstructionMask;  // Walls + Door

    [Header("Ray Origins")]
    public Transform eye;
    public float eyeHeight = 1.6f;

    [Header("Targeting")]
    [Tooltip("If true, aim at the player's collider bounds center (recommended).")]
    public bool aimAtTargetCollider = true;

    [Tooltip("Fallback aim height used only if no collider is found.")]
    public float fallbackPlayerAimHeight = 0.6f;

    [Header("Advanced")]
    public bool ignoreOpenDoors = true;

    [Tooltip("Draw debug ray in Scene view")]
    public bool debugDraw = false;

    [Tooltip("Log the first ray hit (useful to diagnose what blocks LOS)")]
    public bool debugLogFirstHit = false;

    private readonly RaycastHit[] _hits = new RaycastHit[64];

    public bool CanSee(Transform target, out Vector3 lastSeenPoint)
    {
        lastSeenPoint = default;
        if (target == null) return false;

        Vector3 origin = (eye != null) ? eye.position : transform.position + Vector3.up * eyeHeight;

        // ✅ Aim at collider center so we don't miss due to pivot/height mismatch
        Vector3 targetPoint = GetTargetAimPoint(target);

        Vector3 toTarget = targetPoint - origin;
        float dist = toTarget.magnitude;
        if (dist > viewDistance) return false;

        Vector3 dir = toTarget / Mathf.Max(0.0001f, dist);

        // FOV check (flat)
        Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
        if (flatDir.sqrMagnitude < 0.0001f) return false;

        float angle = Vector3.Angle(transform.forward, flatDir.normalized);
        if (angle > viewAngle * 0.5f) return false;

        int mask = obstructionMask | targetMask;

        int hitCount = Physics.RaycastNonAlloc(origin, dir, _hits, viewDistance, mask, QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            if (debugDraw) Debug.DrawRay(origin, dir * viewDistance, Color.red);
            return false;
        }

        Array.Sort(_hits, 0, hitCount, new HitDistanceComparer());

        if (debugDraw) Debug.DrawRay(origin, dir * viewDistance, Color.yellow);

        if (debugLogFirstHit)
        {
            var c0 = _hits[0].collider;
            if (c0 != null)
                Debug.Log($"[MonsterVision] First hit: {c0.name} layer={LayerMask.LayerToName(c0.gameObject.layer)} root={c0.transform.root.name}");
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider c = _hits[i].collider;
            if (c == null) continue;

            // Target?
            if (IsInMask(c.gameObject, targetMask) ||
                (c.attachedRigidbody != null && IsInMask(c.attachedRigidbody.gameObject, targetMask)) ||
                IsAnyParentInMask(c.transform, targetMask))
            {
                lastSeenPoint = _hits[i].point;
                return true;
            }

            // Obstruction?
            if (IsInMask(c.gameObject, obstructionMask) || IsAnyParentInMask(c.transform, obstructionMask))
            {
                if (ignoreOpenDoors)
                {
                    IDoor door = c.GetComponentInParent<IDoor>();
                    if (door != null && door.IsOpen)
                        continue; // open door doesn't block LOS
                }

                return false; // wall or closed door blocks
            }
        }

        return false;
    }

    private Vector3 GetTargetAimPoint(Transform target)
    {
        if (!aimAtTargetCollider)
            return target.position + Vector3.up * fallbackPlayerAimHeight;

        // Find a collider anywhere under the target (your "Body" capsule will be found)
        Collider col = target.GetComponentInChildren<Collider>();
        if (col != null)
            return col.bounds.center;

        return target.position + Vector3.up * fallbackPlayerAimHeight;
    }

    private static bool IsInMask(GameObject go, LayerMask mask)
    {
        int bit = 1 << go.layer;
        return (mask.value & bit) != 0;
    }

    private static bool IsAnyParentInMask(Transform t, LayerMask mask)
    {
        Transform cur = t;
        while (cur != null)
        {
            int bit = 1 << cur.gameObject.layer;
            if ((mask.value & bit) != 0)
                return true;
            cur = cur.parent;
        }
        return false;
    }

    private class HitDistanceComparer : IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}
