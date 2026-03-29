using System.Collections.Generic;
using UnityEngine;

public class MonsterDoorOpener : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask doorMask;
    [SerializeField] private float radius = 1.25f;

    [Header("Behaviour")]
    [SerializeField] private float closeDelay = 0.75f;

    // Tracks doors we opened and when they left our radius
    private readonly Dictionary<IDoor, float> outOfRangeTime = new Dictionary<IDoor, float>();
    private readonly Collider[] hits = new Collider[16];

    private void Update()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, hits, doorMask);

        // Mark all currently in range
        HashSet<IDoor> inRange = null;
        if (count > 0) inRange = new HashSet<IDoor>();

        for (int i = 0; i < count; i++)
        {
            Collider c = hits[i];
            if (c == null) continue;

            IDoor door = c.GetComponentInParent<IDoor>();
            if (door == null) continue;

            inRange?.Add(door);

            // Open it if needed
            if (!door.IsOpen)
                door.OpenDoor(transform);

            // If it was previously "out of range", remove that timer
            if (outOfRangeTime.ContainsKey(door))
                outOfRangeTime.Remove(door);
        }

        // Any door we opened that is now NOT in range starts a close timer
        // (We only close doors that are open)
        List<IDoor> toClose = null;

        // Add newly out-of-range doors
        // We discover them by looking for doors in our dictionary OR storing doors we've seen.
        // Easiest: if a door is open and NOT inRange, start timer.
        // We'll do a second overlap with slightly bigger radius to “learn” doors:
        int learnCount = Physics.OverlapSphereNonAlloc(transform.position, radius * 1.8f, hits, doorMask);
        for (int i = 0; i < learnCount; i++)
        {
            Collider c = hits[i];
            if (c == null) continue;

            IDoor door = c.GetComponentInParent<IDoor>();
            if (door == null) continue;

            if (!door.IsOpen) continue;

            bool isInRange = inRange != null && inRange.Contains(door);
            if (isInRange) continue;

            if (!outOfRangeTime.ContainsKey(door))
                outOfRangeTime[door] = Time.time;
        }

        // Close doors whose timer has expired (and still out of range)
        if (outOfRangeTime.Count > 0)
        {
            var keys = new List<IDoor>(outOfRangeTime.Keys);
            foreach (var door in keys)
            {
                if (door == null)
                {
                    outOfRangeTime.Remove(door);
                    continue;
                }

                // still out of range?
                bool stillOut = true;

                if (inRange != null && inRange.Contains(door))
                    stillOut = false;

                if (!stillOut)
                {
                    outOfRangeTime.Remove(door);
                    continue;
                }

                float t0 = outOfRangeTime[door];
                if (Time.time - t0 >= closeDelay)
                {
                    toClose ??= new List<IDoor>();
                    toClose.Add(door);
                }
            }
        }

        if (toClose != null)
        {
            foreach (var door in toClose)
            {
                if (door != null && door.IsOpen)
                    door.CloseDoor();

                outOfRangeTime.Remove(door);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
