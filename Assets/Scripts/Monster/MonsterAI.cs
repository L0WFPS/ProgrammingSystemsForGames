using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MonsterAI : MonoBehaviour
{
    [Header("References")]
    public ProceduralLevelGenerator generator;
    public Transform player;
    public MonsterVision vision;

    [Header("Speeds")]
    public float patrolSpeed = 2.5f;
    public float chaseSpeed = 5f;

    [Header("Combat")]
    public float killDistance = 1.2f;

    [Header("Pathfinding / Timing")]
    [Tooltip("How long LOS must be lost before switching from Chase to Search.")]
    public float loseSightDelay = 0.25f;

    // ---------- DEBUG (Inspector) ----------
    [Header("===== DEBUG =====")]
    [SerializeField] private string debugState;
    [SerializeField] private bool debugCanSeePlayer;
    [SerializeField] private string debugMonsterRoom;
    [SerializeField] private string debugPlayerRoom;
    [SerializeField] private string debugLastKnownRoom;
    [SerializeField] private int debugPathCount;
    [SerializeField] private int debugPathIndex;
    [TextArea]
    [SerializeField] private string debugNotes;

    private enum State { Patrol, Chase, Search }
    private State currentState = State.Patrol;

    // Shared path for Patrol + Search
    private List<ProceduralLevelGenerator.RoomNode> path;
    private int pathIndex;

    // Last known room where the player was seen
    private ProceduralLevelGenerator.RoomNode lastKnownPlayerRoom;

    // Timers
    private float lostSightTimer;

    private Rigidbody rb;

    // ---------- UNITY LIFECYCLE ----------

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.useGravity = true;
        rb.isKinematic = false;
    }

    void Start()
    {
        path = null;
        pathIndex = 0;
        currentState = State.Patrol;
        lostSightTimer = 0f;
        debugNotes = "MonsterAI started.";
    }

    void Update()
    {
        if (generator == null || player == null || vision == null)
        {
            debugNotes = "Missing reference(s): generator/player/vision.";
            return;
        }

        var rooms = generator.GetRooms();
        if (rooms == null || rooms.Count == 0)
        {
            debugNotes = "No rooms yet. Waiting for level generation.";
            return;
        }

        // Compute LOS ONCE per frame to avoid ping-pong behaviour.
        bool canSee = vision.CanSee(player);

        UpdateDebugPanel(canSee);

        switch (currentState)
        {
            case State.Patrol: Patrol(canSee); break;
            case State.Chase: Chase(canSee); break;
            case State.Search: Search(canSee); break;
        }

        TryKillPlayer();
    }

    // ---------- DEBUG ----------

    void UpdateDebugPanel(bool canSee)
    {
        debugState = currentState.ToString();
        debugCanSeePlayer = canSee;

        var monsterRoom = generator.GetClosestRoom(transform.position);
        var playerRoom = generator.GetClosestRoom(player.position);

        debugMonsterRoom = monsterRoom != null ? monsterRoom.gridPos.ToString() : "null";
        debugPlayerRoom = playerRoom != null ? playerRoom.gridPos.ToString() : "null";
        debugLastKnownRoom = lastKnownPlayerRoom != null ? lastKnownPlayerRoom.gridPos.ToString() : "null";

        debugPathCount = (path != null ? path.Count : 0);
        debugPathIndex = pathIndex;
    }

    // ---------- PATROL ----------

    void Patrol(bool canSee)
    {
        if (canSee)
        {
            // Immediately switch to chase on sight.
            currentState = State.Chase;
            lostSightTimer = 0f;
            path = null;
            pathIndex = 0;
            debugNotes = "PATROL → CHASE (saw player).";
            return;
        }

        if (path == null || pathIndex >= (path?.Count ?? 0))
        {
            SetNewPatrolPath();
        }

        FollowPath(patrolSpeed);
    }

    void SetNewPatrolPath()
    {
        var rooms = generator.GetRooms();
        var currentRoom = generator.GetClosestRoom(transform.position);

        if (rooms == null || rooms.Count == 0 || currentRoom == null)
        {
            debugNotes = "SetNewPatrolPath: no rooms or currentRoom null.";
            return;
        }

        // Pick a random different room
        ProceduralLevelGenerator.RoomNode target = currentRoom;
        int safety = 0;
        while (target == currentRoom && safety < 20)
        {
            target = rooms[Random.Range(0, rooms.Count)];
            safety++;
        }

        path = RoomPathfinder.FindPath(currentRoom, target);
        pathIndex = 0;

        debugNotes = "New PATROL path. Length: " + (path != null ? path.Count.ToString() : "null");
    }

    // ---------- CHASE (DIRECT CHASE WHEN LOS TRUE) ----------

    void Chase(bool canSee)
    {
        var playerRoom = generator.GetClosestRoom(player.position);
        if (canSee)
        {
            // Reset lost sight timer
            lostSightTimer = 0f;

            // Update last known player room
            lastKnownPlayerRoom = playerRoom;

            // **DIRECT CHASE**: always move straight towards the player while LOS is true.
            DirectChase();
            debugNotes = "CHASE (direct) – LOS active.";
            return;
        }

        // LOS is false here
        lostSightTimer += Time.deltaTime;

        // Short grace delay to prevent flicker at corners
        if (lostSightTimer < loseSightDelay)
        {
            // Still “kind of chasing” – but we don’t have LOS, so just do nothing special.
            debugNotes = "CHASE – LOS just lost, waiting (grace period).";
            return;
        }

        // LOS really lost → switch to SEARCH around lastKnownPlayerRoom
        if (lastKnownPlayerRoom == null)
        {
            lastKnownPlayerRoom = playerRoom; // Fallback if we somehow never set it
        }

        currentState = State.Search;
        path = null;
        pathIndex = 0;
        debugNotes = "CHASE → SEARCH (lost LOS, going to last known room).";
    }

    void DirectChase()
    {
        Vector3 dir = (player.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        dir.Normalize();

        Vector3 newPos = transform.position + dir * chaseSpeed * Time.deltaTime;
        rb.MovePosition(newPos);

        if (dir != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 10f * Time.deltaTime);
        }
    }

    // ---------- SEARCH (MAZE-LOCKED) ----------

    void Search(bool canSee)
    {
        if (canSee)
        {
            // Found player again, go straight to chase.
            currentState = State.Chase;
            lostSightTimer = 0f;
            path = null;
            pathIndex = 0;
            debugNotes = "SEARCH → CHASE (player seen).";
            return;
        }

        if (lastKnownPlayerRoom == null)
        {
            // Nothing to search → back to patrol
            currentState = State.Patrol;
            path = null;
            pathIndex = 0;
            debugNotes = "SEARCH: no lastKnownPlayerRoom → PATROL.";
            return;
        }

        if (path == null || pathIndex >= (path?.Count ?? 0))
        {
            var monsterRoom = generator.GetClosestRoom(transform.position);
            if (monsterRoom == null)
            {
                currentState = State.Patrol;
                debugNotes = "SEARCH: monsterRoom null → PATROL.";
                return;
            }

            path = RoomPathfinder.FindPath(monsterRoom, lastKnownPlayerRoom);
            pathIndex = 0;

            if (path == null || path.Count == 0)
            {
                currentState = State.Patrol;
                path = null;
                debugNotes = "SEARCH: no path to lastKnownPlayerRoom → PATROL.";
                return;
            }

            debugNotes = "SEARCH: path to lastKnownPlayerRoom. Length: " + path.Count;
        }

        FollowPath(patrolSpeed);

        // When we've walked the path to the last known room and still don't see the player, give up and patrol.
        if (pathIndex >= (path?.Count ?? 0))
        {
            currentState = State.Patrol;
            path = null;
            pathIndex = 0;
            debugNotes = "SEARCH: reached lastKnownPlayerRoom, player not found → PATROL.";
        }
    }

    // ---------- SHARED MAZE PATH FOLLOWING ----------

    void FollowPath(float speed)
    {
        if (path == null || pathIndex >= path.Count)
            return;

        var node = path[pathIndex];

        // World position for this room centre
        Vector3 target = new Vector3(
            node.gridPos.x * generator.cellSize,
            transform.position.y,
            node.gridPos.y * generator.cellSize
        );

        Vector3 delta = target - transform.position;
        delta.y = 0f;

        if (delta.sqrMagnitude < 0.0001f)
        {
            pathIndex++;
            return;
        }

        Vector3 dir = delta.normalized;
        Vector3 newPos = transform.position + dir * speed * Time.deltaTime;

        rb.MovePosition(newPos);

        if (dir != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 6f * Time.deltaTime);
        }

        // Advance to next node when close enough
        if (Vector3.Distance(transform.position, target) < 0.25f)
        {
            pathIndex++;
        }
    }

    // ---------- KILL PLAYER ----------

    void TryKillPlayer()
    {
        if (player == null) return;

        if (Vector3.Distance(transform.position, player.position) <= killDistance)
        {
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }
    }
}
