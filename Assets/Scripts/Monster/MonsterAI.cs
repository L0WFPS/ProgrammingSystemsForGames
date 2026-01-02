using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody))]
public class MonsterAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Search }
    private enum SegmentPhase { ToDoor, ToCenter }

    [Header("Auto Wiring")]
    public bool autoFindReferences = true;

    [Header("References")]
    public ProceduralLevelGenerator generator;
    public Transform player;
    public MonsterVision vision;

    [Header("Movement")]
    public float patrolSpeed = 2.5f;
    public float chaseSpeed = 5.0f;
    public float turnSpeed = 8f;
    public float arriveCenterDist = 0.35f;
    public float arriveDoorDist = 0.55f;

    [Header("Grounding")]
    public bool snapToGround = true;
    public LayerMask groundMask;
    public float groundRayHeight = 2.0f;
    public float groundRayDistance = 6.0f;
    public float groundOffset = 1.08f;

    [Header("Blocking (used for patrol + searching paths)")]
    [Tooltip("Walls + Door layers.")]
    public LayerMask movementBlockMask;
    public float probeRadius = 0.35f;
    public float blockedTimeout = 0.8f;

    [Header("Chase")]
    public float chaseRepathInterval = 0.2f;
    public float loseSightGrace = 0.7f;

    [Header("Search (improved)")]
    public float searchDuration = 8f;
    public int forwardSearchSteps = 2;
    public bool includeSideChecks = true;

    [Header("Combat")]
    public float killDistance = 1.2f;

    [Header("Debug")]
    [SerializeField] private State currentState = State.Patrol;
    [SerializeField] private string missingRefsDebug = "";

    private Rigidbody rb;

    // Path (rooms)
    private List<ProceduralLevelGenerator.RoomNode> path;
    private int pathIndex;

    // Patrol
    private ProceduralLevelGenerator.RoomNode patrolCurrent;
    private ProceduralLevelGenerator.RoomNode patrolPrev;
    private ProceduralLevelGenerator.RoomNode patrolNext;

    // Segment movement (door midpoint then center)
    private SegmentPhase segPhase = SegmentPhase.ToDoor;
    private Vector3 segFrom, segTo, segDoorMid;
    private float blockedTimer;

    // Timers
    private float repathTimer;
    private float lostSightTimer;
    private float searchTimer;

    // Vision cache (Update -> FixedUpdate)
    private bool canSeeCached;
    private Vector3 seenPointCached;

    // Last seen
    private Vector3 lastSeenPlayerPos;
    private Vector3 prevSeenPlayerPos;
    private Vector3 lastSeenPlayerDir;
    private ProceduralLevelGenerator.RoomNode lastSeenRoom;

    // Search plan
    private readonly List<ProceduralLevelGenerator.RoomNode> searchGoals = new();
    private int searchGoalIndex = 0;

    // Grounded Y cache
    private float groundedY;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Recommended with snapToGround + MovePosition
        if (snapToGround)
            rb.useGravity = false;
    }

    private void Start()
    {
        TryAutoWire();

        if (generator == null)
        {
            Debug.LogError("[MonsterAI] No ProceduralLevelGenerator assigned/found.");
            enabled = false;
            return;
        }

        // Auto set ground mask if missing
        if (snapToGround && groundMask.value == 0)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                groundMask = LayerMask.GetMask("Ground");
        }

        groundedY = transform.position.y;

        patrolCurrent = generator.GetClosestRoom(transform.position);
        patrolPrev = null;
        ChooseNextPatrolNode();
        ResetSegmentForPatrol();

        EnterPatrol();
    }

    private void TryAutoWire()
    {
        if (!autoFindReferences) return;

        if (generator == null)
            generator = FindObjectOfType<ProceduralLevelGenerator>();

        if (vision == null)
            vision = GetComponent<MonsterVision>();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    private void Update()
    {
        if (generator == null || player == null || vision == null)
        {
            missingRefsDebug = $"generator={(generator != null)} player={(player != null)} vision={(vision != null)}";
            return;
        }
        missingRefsDebug = "";

        // Kill check (flat)
        if (Vector3.Distance(Flat(transform.position), Flat(player.position)) <= killDistance)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // Cache vision results so FixedUpdate uses the same "frame" info
        canSeeCached = vision.CanSee(player, out seenPointCached);

        switch (currentState)
        {
            case State.Patrol:
                if (canSeeCached)
                {
                    UpdateLastSeenData(seenPointCached);
                    EnterChase();
                }
                break;

            case State.Chase:
                if (canSeeCached)
                {
                    lostSightTimer = 0f;
                    UpdateLastSeenData(seenPointCached);
                }
                else
                {
                    lostSightTimer += Time.deltaTime;
                    if (lostSightTimer >= loseSightGrace)
                        EnterSearch();
                }
                break;

            case State.Search:
                if (canSeeCached)
                {
                    UpdateLastSeenData(seenPointCached);
                    EnterChase();
                }
                else
                {
                    searchTimer += Time.deltaTime;
                    if (searchTimer >= searchDuration)
                        EnterPatrol();
                }
                break;
        }
    }

    private void FixedUpdate()
    {
        if (generator == null) return;

        if (snapToGround)
            SnapYToGround();

        switch (currentState)
        {
            case State.Patrol: TickPatrol(patrolSpeed); break;
            case State.Chase: TickChase(chaseSpeed); break;
            case State.Search: TickSearch(patrolSpeed); break;
        }
    }

    // ---------------- STATES ----------------

    private void EnterPatrol()
    {
        currentState = State.Patrol;

        path = null;
        pathIndex = 0;

        repathTimer = 0f;
        lostSightTimer = 0f;
        searchTimer = 0f;

        searchGoals.Clear();
        searchGoalIndex = 0;

        patrolCurrent = generator.GetClosestRoom(transform.position);
        patrolPrev = null;
        ChooseNextPatrolNode();
        ResetSegmentForPatrol();
    }

    private void EnterChase()
    {
        currentState = State.Chase;
        lostSightTimer = 0f;
        repathTimer = 999f; // force immediate repath
        blockedTimer = 0f;
        segPhase = SegmentPhase.ToDoor;

        path = null;
        pathIndex = 0;
    }

    private void EnterSearch()
    {
        currentState = State.Search;
        searchTimer = 0f;

        lastSeenRoom = generator.GetClosestRoom(lastSeenPlayerPos);

        BuildSearchPlan(lastSeenRoom, lastSeenPlayerDir);

        searchGoalIndex = 0;
        if (searchGoals.Count > 0)
            BuildPathTo(searchGoals[searchGoalIndex]);
        else
            EnterPatrol();
    }

    // ---------------- PATROL ----------------

    private void TickPatrol(float speed)
    {
        if (patrolCurrent == null)
            patrolCurrent = generator.GetClosestRoom(transform.position);

        if (patrolNext == null)
            ChooseNextPatrolNode();

        if (patrolNext == null) return;

        Vector3 curPos = generator.GridToWorldPublic(patrolCurrent.gridPos);
        Vector3 nextPos = generator.GridToWorldPublic(patrolNext.gridPos);

        if ((Flat(segFrom) - Flat(curPos)).sqrMagnitude > 0.001f ||
            (Flat(segTo) - Flat(nextPos)).sqrMagnitude > 0.001f)
        {
            SetSegment(curPos, nextPos);
        }

        bool reachedNextCenter = MoveAlongSegment(speed);

        if (reachedNextCenter)
        {
            patrolPrev = patrolCurrent;
            patrolCurrent = patrolNext;
            ChooseNextPatrolNode();
            ResetSegmentForPatrol();
        }
    }

    private void ChooseNextPatrolNode()
    {
        if (patrolCurrent == null || patrolCurrent.neighbors == null || patrolCurrent.neighbors.Count == 0)
        {
            patrolNext = null;
            return;
        }

        var options = patrolCurrent.neighbors;

        if (options.Count == 1)
        {
            patrolNext = options[0];
            return;
        }

        List<ProceduralLevelGenerator.RoomNode> candidates = new();
        foreach (var n in options)
            if (n != null && n != patrolPrev) candidates.Add(n);

        if (candidates.Count == 0)
            candidates = options;

        patrolNext = candidates[Random.Range(0, candidates.Count)];
    }

    private void ResetSegmentForPatrol()
    {
        if (patrolCurrent == null || patrolNext == null) return;
        Vector3 a = generator.GridToWorldPublic(patrolCurrent.gridPos);
        Vector3 b = generator.GridToWorldPublic(patrolNext.gridPos);
        SetSegment(a, b);
    }

    // ---------------- CHASE ----------------

    private void TickChase(float speed)
    {
        if (player == null) return;

        // ✅ If we can see the player right now, ALWAYS move directly toward them.
        // This guarantees we push through doorways/corridors while LOS exists.
        if (canSeeCached)
        {
            MoveDirect(player.position, speed);
            return;
        }

        // If LOS is lost but still in Chase (grace period), pursue last seen position.
        Vector3 goalWorldPos = lastSeenPlayerPos;

        var startRoom = generator.GetClosestRoom(transform.position);
        var goalRoom = generator.GetClosestRoom(goalWorldPos);

        repathTimer += Time.fixedDeltaTime;

        bool needRepath = (repathTimer >= chaseRepathInterval);

        if (path != null && path.Count > 0 && goalRoom != null)
        {
            var lastGoal = path[path.Count - 1];
            if (lastGoal != null && lastGoal != goalRoom)
                needRepath = true;
        }
        else
        {
            needRepath = true;
        }

        if (needRepath)
        {
            repathTimer = 0f;
            BuildPathAStar(startRoom, goalRoom);
        }

        // Fallback: if no path, still move toward last seen position
        if (path == null || path.Count == 0)
        {
            MoveDirect(goalWorldPos, speed);
            return;
        }

        FollowPathStable(speed);
    }

    // Direct movement used during LOS chase (no block probe = no doorway-frame stalls)
    private void MoveDirect(Vector3 targetWorldPos, float speed)
    {
        Vector3 pos = rb.position;
        Vector3 to = targetWorldPos - pos;
        to.y = 0f;

        if (to.sqrMagnitude < 0.0001f) return;

        Vector3 dir = to.normalized;

        float step = speed * Time.fixedDeltaTime;
        Vector3 nextPos = pos + dir * step;
        nextPos.y = groundedY;

        rb.MovePosition(nextPos);

        Quaternion desired = Quaternion.LookRotation(dir, Vector3.up);
        Quaternion rot = Quaternion.Slerp(rb.rotation, desired, turnSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(rot);
    }

    // ---------------- SEARCH (improved) ----------------

    private void TickSearch(float speed)
    {
        if (path == null || pathIndex >= path.Count)
        {
            searchGoalIndex++;

            if (searchGoalIndex >= searchGoals.Count)
            {
                EnterPatrol();
                return;
            }

            BuildPathTo(searchGoals[searchGoalIndex]);
            return;
        }

        FollowPathStable(speed);
    }

    private void BuildSearchPlan(ProceduralLevelGenerator.RoomNode startRoom, Vector3 dir)
    {
        searchGoals.Clear();
        if (startRoom == null) return;

        // 1) Always go to last seen room
        searchGoals.Add(startRoom);

        // 2) Push forward rooms in last known direction
        var cur = startRoom;
        for (int i = 0; i < forwardSearchSteps; i++)
        {
            var next = PickNeighborInDirection(cur, dir);
            if (next == null || searchGoals.Contains(next)) break;
            searchGoals.Add(next);
            cur = next;
        }

        // 3) Optional side checks at the forward endpoint
        if (includeSideChecks && cur != null && cur.neighbors != null)
        {
            foreach (var n in cur.neighbors)
            {
                if (n == null) continue;
                if (searchGoals.Contains(n)) continue;
                searchGoals.Add(n);
            }
        }
    }

    private ProceduralLevelGenerator.RoomNode PickNeighborInDirection(ProceduralLevelGenerator.RoomNode from, Vector3 direction)
    {
        if (from == null || from.neighbors == null || from.neighbors.Count == 0)
            return null;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return from.neighbors[0];

        float bestDot = -999f;
        ProceduralLevelGenerator.RoomNode best = null;

        Vector3 fromPos = generator.GridToWorldPublic(from.gridPos);

        foreach (var n in from.neighbors)
        {
            if (n == null) continue;

            Vector3 nPos = generator.GridToWorldPublic(n.gridPos);
            Vector3 toN = nPos - fromPos;
            toN.y = 0f;
            if (toN.sqrMagnitude < 0.0001f) continue;

            float d = Vector3.Dot(direction.normalized, toN.normalized);
            if (d > bestDot)
            {
                bestDot = d;
                best = n;
            }
        }

        return best;
    }

    // ---------------- PATH ----------------

    private void BuildPathTo(ProceduralLevelGenerator.RoomNode goal)
    {
        var start = generator.GetClosestRoom(transform.position);
        BuildPathAStar(start, goal);
    }

    private void BuildPathAStar(ProceduralLevelGenerator.RoomNode start, ProceduralLevelGenerator.RoomNode goal)
    {
        if (start == null || goal == null)
        {
            path = null;
            pathIndex = 0;
            return;
        }

        path = RoomPathfinder.FindPath(start, goal);
        pathIndex = 0;

        segPhase = SegmentPhase.ToDoor;
        blockedTimer = 0f;
    }

    private void FollowPathStable(float speed)
    {
        if (path == null || pathIndex >= path.Count) return;

        // First node: go to its center
        if (pathIndex == 0)
        {
            Vector3 first = generator.GridToWorldPublic(path[pathIndex].gridPos);
            bool reached = MoveTowardsWithBlock(first, speed, arriveCenterDist, allowWaitAtDoor: false);
            if (reached) pathIndex++;
            return;
        }

        Vector3 prev = generator.GridToWorldPublic(path[pathIndex - 1].gridPos);
        Vector3 cur = generator.GridToWorldPublic(path[pathIndex].gridPos);

        if ((Flat(segFrom) - Flat(prev)).sqrMagnitude > 0.001f ||
            (Flat(segTo) - Flat(cur)).sqrMagnitude > 0.001f)
        {
            SetSegment(prev, cur);
        }

        bool reachedCenter = MoveAlongSegment(speed);
        if (reachedCenter)
        {
            pathIndex++;
            blockedTimer = 0f;
            segPhase = SegmentPhase.ToDoor;
        }
    }

    // ---------------- MOVEMENT ----------------

    private void SetSegment(Vector3 from, Vector3 to)
    {
        segFrom = from;
        segTo = to;

        segDoorMid = (from + to) * 0.5f;
        segDoorMid.y = rb.position.y;

        segPhase = SegmentPhase.ToDoor;
        blockedTimer = 0f;
    }

    private bool MoveAlongSegment(float speed)
    {
        // Only "wait at door" in patrol.
        bool allowWaitAtDoor = (currentState == State.Patrol);

        if (segPhase == SegmentPhase.ToDoor)
        {
            bool reachedDoor = MoveTowardsWithBlock(segDoorMid, speed, arriveDoorDist, allowWaitAtDoor);
            if (reachedDoor)
            {
                segPhase = SegmentPhase.ToCenter;
                blockedTimer = 0f;
            }
            return false;
        }

        return MoveTowardsWithBlock(segTo, speed, arriveCenterDist, allowWaitAtDoor: false);
    }

    // Door-aware blocking: OPEN doors do not block movement casts.
    private bool MoveTowardsWithBlock(Vector3 target, float speed, float arriveDist, bool allowWaitAtDoor)
    {
        Vector3 pos = rb.position;
        Vector3 to = target - pos;
        to.y = 0f;

        if (to.sqrMagnitude <= arriveDist * arriveDist)
            return true;

        Vector3 dir = to.normalized;

        float step = speed * Time.fixedDeltaTime;
        float castDist = Mathf.Min(step + 0.05f, to.magnitude);

        bool blocked = IsBlockedBySolid(pos + Vector3.up * 0.5f, dir, castDist);

        if (blocked)
        {
            blockedTimer += Time.fixedDeltaTime;

            if (allowWaitAtDoor)
            {
                RotateTowards(dir);
                return false;
            }

            if (blockedTimer >= blockedTimeout)
            {
                blockedTimer = 0f;

                if (currentState == State.Patrol)
                {
                    ChooseNextPatrolNode();
                    ResetSegmentForPatrol();
                }
                else
                {
                    repathTimer = 999f;
                    path = null;
                    pathIndex = 0;
                }
            }

            RotateTowards(dir);
            return false;
        }

        blockedTimer = 0f;

        Vector3 nextPos = pos + dir * step;
        nextPos.y = groundedY;
        rb.MovePosition(nextPos);

        RotateTowards(dir);
        return false;
    }

    private bool IsBlockedBySolid(Vector3 origin, Vector3 dir, float dist)
    {
        RaycastHit[] hits = Physics.SphereCastAll(origin, probeRadius, dir, dist, movementBlockMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        foreach (var h in hits)
        {
            if (h.collider == null) continue;

            IDoor door = h.collider.GetComponentInParent<IDoor>();
            if (door != null)
            {
                if (door.IsOpen) continue;
                return true;
            }

            return true;
        }

        return false;
    }

    private void RotateTowards(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion desired = Quaternion.LookRotation(dir, Vector3.up);
        Quaternion rot = Quaternion.Slerp(rb.rotation, desired, turnSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(rot);
    }

    private void SnapYToGround()
    {
        if (groundMask.value == 0)
        {
            groundedY = rb.position.y;
            return;
        }

        Vector3 origin = rb.position + Vector3.up * groundRayHeight;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
            groundRayHeight + groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundedY = hit.point.y + groundOffset;
            Vector3 p = rb.position;
            p.y = groundedY;
            rb.MovePosition(p);
        }
        else
        {
            groundedY = rb.position.y;
        }
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private void UpdateLastSeenData(Vector3 seenPoint)
    {
        lastSeenPlayerPos = (seenPoint != default) ? seenPoint : player.position;
        lastSeenRoom = generator.GetClosestRoom(lastSeenPlayerPos);

        Vector3 deltaMove = player.position - prevSeenPlayerPos;
        deltaMove.y = 0f;

        if (deltaMove.sqrMagnitude > 0.0001f)
        {
            lastSeenPlayerDir = deltaMove.normalized;
        }
        else
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
                lastSeenPlayerDir = toPlayer.normalized;
        }

        prevSeenPlayerPos = player.position;
    }
}
