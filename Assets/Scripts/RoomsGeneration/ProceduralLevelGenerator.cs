using System.Collections.Generic;
using System;
using UnityEngine;

public class ProceduralLevelGenerator : MonoBehaviour
{
    public enum RoomKind
    {
        Entrance,
        Normal,
        Objective
    }

    [Serializable]
    public class RoomNode
    {
        public Vector2Int gridPos;
        public RoomKind kind = RoomKind.Normal;
        public List<RoomNode> neighbors = new List<RoomNode>();

        // Optional: link to the instantiated GameObject for this room
        [NonSerialized] public GameObject instance;
    }

    [Header("Layout Settings")]
    [Min(2)]
    public int mainPathLength = 10;

    public int cellSize = 10;          // World units between room centers
    public bool useRandomSeed = true;
    public int fixedSeed = 0;

    [Header("Branch Settings")]
    [Range(0f, 1f)]
    public float branchChancePerRoom = 0.5f;

    [Min(1)]
    public int minBranchLength = 1;

    [Min(1)]
    public int maxBranchLength = 3;

    [Min(0)]
    public int maxBranches = 4;

    [Min(1)]
    public int maxTotalRooms = 40;

    [Header("Prefabs")]
    public Transform levelRoot;
    public GameObject entrancePrefab;
    public GameObject normalPrefab;
    public GameObject objectivePrefab;

    [Header("Debug")]
    public bool regenerateOnStart = true;
    public bool drawGizmos = true;

    [SerializeField]
    private List<RoomNode> rooms = new List<RoomNode>();

    // For quick overlap checks
    private Dictionary<Vector2Int, RoomNode> roomLookup = new Dictionary<Vector2Int, RoomNode>();

    // Track spawned room instances so we can clean them up
    private readonly List<GameObject> spawnedInstances = new List<GameObject>();

    private void Start()
    {
        if (regenerateOnStart)
        {
            GenerateLayout();
        }
    }

    [ContextMenu("Generate Layout")]
    public void GenerateLayout()
    {
        // Clear data
        rooms.Clear();
        roomLookup.Clear();

        // Clear old geometry
        ClearGeometry();

        System.Random rng = useRandomSeed
            ? new System.Random()
            : new System.Random(fixedSeed);

        // 1. Create entrance at (0,0)
        Vector2Int currentPos = Vector2Int.zero;
        RoomNode entrance = CreateRoom(currentPos, RoomKind.Entrance);
        RoomNode lastRoom = entrance;

        // 2. Extend main path
        for (int i = 1; i < mainPathLength; i++)
        {
            if (rooms.Count >= maxTotalRooms)
                break;

            Vector2Int nextPos = FindNextStep(currentPos, rng);

            // If we couldn't find a free neighbor, stop extending
            if (nextPos == currentPos)
            {
                Debug.LogWarning("Could not find a new cell for the main path step " + i);
                break;
            }

            RoomNode newRoom = CreateRoom(nextPos, RoomKind.Normal);

            // Link neighbors both ways
            lastRoom.neighbors.Add(newRoom);
            newRoom.neighbors.Add(lastRoom);

            lastRoom = newRoom;
            currentPos = nextPos;
        }

        int mainPathCount = rooms.Count;

        // 3. Add side branches
        GenerateBranches(mainPathCount, rng);

        // 4. Mark the last room of the main path as Objective
        if (mainPathCount > 1)
        {
            lastRoom.kind = RoomKind.Objective;
        }

        Debug.Log($"Generated layout with {rooms.Count} rooms " +
                  $"({mainPathCount} on main path, branches added).");

        // 5. Build actual room instances in the scene
        BuildLevelGeometry();
    }

    private RoomNode CreateRoom(Vector2Int pos, RoomKind kind)
    {
        RoomNode room = new RoomNode
        {
            gridPos = pos,
            kind = kind
        };

        rooms.Add(room);
        roomLookup[pos] = room;
        return room;
    }

    private Vector2Int FindNextStep(Vector2Int currentPos, System.Random rng)
    {
        // Four cardinal directions: up, down, left, right in grid space
        Vector2Int[] directions =
        {
            new Vector2Int( 1,  0),
            new Vector2Int(-1,  0),
            new Vector2Int( 0,  1),
            new Vector2Int( 0, -1),
        };

        // Shuffle directions (Fisher-Yates)
        for (int i = 0; i < directions.Length - 1; i++)
        {
            int j = rng.Next(i, directions.Length);
            (directions[i], directions[j]) = (directions[j], directions[i]);
        }

        // Try each direction in random order
        foreach (var dir in directions)
        {
            Vector2Int candidate = currentPos + dir;
            if (!roomLookup.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        // If everything around is occupied, return current as fallback
        return currentPos;
    }

    private void GenerateBranches(int mainPathCount, System.Random rng)
    {
        int branchesCreated = 0;

        // We don't branch from the very last main-path room (objective end)
        for (int i = 0; i < mainPathCount - 1; i++)
        {
            if (branchesCreated >= maxBranches)
                break;

            if (rooms.Count >= maxTotalRooms)
                break;

            RoomNode baseRoom = rooms[i];

            // Roll to see if we spawn a branch here
            if (rng.NextDouble() > branchChancePerRoom)
                continue;

            int targetLength = rng.Next(minBranchLength, maxBranchLength + 1);
            CreateBranchFrom(baseRoom, targetLength, ref branchesCreated, rng);
        }
    }

    private void CreateBranchFrom(RoomNode baseRoom, int targetLength, ref int branchesCreated, System.Random rng)
    {
        Vector2Int currentPos = baseRoom.gridPos;
        RoomNode previous = baseRoom;

        for (int step = 0; step < targetLength; step++)
        {
            if (rooms.Count >= maxTotalRooms)
                break;

            Vector2Int nextPos = FindNextStep(currentPos, rng);

            // If we couldn't find a free neighbor, stop this branch
            if (nextPos == currentPos)
                break;

            if (roomLookup.ContainsKey(nextPos))
                break;

            RoomNode newRoom = CreateRoom(nextPos, RoomKind.Normal);

            // Link both ways
            previous.neighbors.Add(newRoom);
            newRoom.neighbors.Add(previous);

            previous = newRoom;
            currentPos = nextPos;
        }

        branchesCreated++;
    }

    // ---------- GEOMETRY LAYER ----------

    private void ClearGeometry()
    {
        // Destroy previously spawned instances
        foreach (var obj in spawnedInstances)
        {
            if (obj == null) continue;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        spawnedInstances.Clear();

        // Optional: also clear children under levelRoot
        if (levelRoot != null)
        {
            for (int i = levelRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = levelRoot.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
    }

    private void BuildLevelGeometry()
    {
        Transform parent = levelRoot != null ? levelRoot : transform;

        foreach (var room in rooms)
        {
            Vector3 pos = GridToWorld(room.gridPos);

            // -------------------------------
            // CREATE FLOOR WITH COLLIDER
            // -------------------------------
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(parent);
            floor.transform.position = pos + new Vector3(0, -0.5f, 0); // a little downward so player stands on top
            floor.transform.localScale = new Vector3(cellSize, 1f, cellSize);
            floor.name = "Floor_" + room.gridPos;
            floor.layer = LayerMask.NameToLayer("Ground");



            // store as room instance
            room.instance = floor;
            spawnedInstances.Add(floor);

            // -------------------------------
            // CREATE WALLS AROUND ROOM
            // -------------------------------
            float half = cellSize / 2f;
            float wallHeight = 3f;
            float wallThickness = 0.5f;

            // North
            if (!room.neighbors.Exists(r => r.gridPos == room.gridPos + Vector2Int.up))
                CreateWall(parent, pos + new Vector3(0, wallHeight / 2f, half),
                           cellSize, wallHeight, wallThickness);

            // South
            if (!room.neighbors.Exists(r => r.gridPos == room.gridPos + Vector2Int.down))
                CreateWall(parent, pos + new Vector3(0, wallHeight / 2f, -half),
                           cellSize, wallHeight, wallThickness);

            // East
            if (!room.neighbors.Exists(r => r.gridPos == room.gridPos + Vector2Int.right))
                CreateWall(parent, pos + new Vector3(half, wallHeight / 2f, 0),
                           wallThickness, wallHeight, cellSize);

            // West
            if (!room.neighbors.Exists(r => r.gridPos == room.gridPos + Vector2Int.left))
                CreateWall(parent, pos + new Vector3(-half, wallHeight / 2f, 0),
                           wallThickness, wallHeight, cellSize);
        }
    }

    // Helper method to spawn a wall cube
    private void CreateWall(Transform parent, Vector3 pos, float sizeX, float sizeY, float sizeZ)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.SetParent(parent);
        wall.transform.position = pos;
        wall.transform.localScale = new Vector3(sizeX, sizeY, sizeZ);
        wall.name = "Wall";

        wall.layer = LayerMask.NameToLayer("Walls");



        spawnedInstances.Add(wall);
    }


    private GameObject GetPrefabForRoom(RoomKind kind)
    {
        switch (kind)
        {
            case RoomKind.Entrance:
                return entrancePrefab;
            case RoomKind.Objective:
                // Fallback to normal if objectivePrefab is not set
                return objectivePrefab != null ? objectivePrefab : normalPrefab;
            default:
                return normalPrefab;
        }
    }

    // ---------- GIZMOS ----------

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        if (rooms == null)
            return;

        foreach (var room in rooms)
        {
            Vector3 worldPos = GridToWorld(room.gridPos);

            // Choose color based on type
            switch (room.kind)
            {
                case RoomKind.Entrance:
                    Gizmos.color = Color.green;
                    break;
                case RoomKind.Objective:
                    Gizmos.color = Color.red;
                    break;
                default:
                    Gizmos.color = Color.cyan;
                    break;
            }

            Vector3 size = new Vector3(cellSize * 0.9f, 2f, cellSize * 0.9f);
            Gizmos.DrawWireCube(worldPos, size);

            // Draw connections to neighbors as lines
            Gizmos.color = Color.white;
            foreach (var neighbor in room.neighbors)
            {
                Vector3 neighborPos = GridToWorld(neighbor.gridPos);
                Gizmos.DrawLine(worldPos, neighborPos);
            }
        }
    }

    public Vector3 GridToWorld(Vector2Int gridPos)

    {
        return new Vector3(gridPos.x * cellSize, 1f, gridPos.y * cellSize);
    }

    // Monster Sighting Support

    public List<RoomNode> GetRooms()
    {
        return rooms;
    }

    public RoomNode GetClosestRoom(Vector3 worldPos)
    {
        RoomNode closest = null;
        float bestDist = float.MaxValue;

        foreach (var room in rooms)
        {
            Vector3 pos = GridToWorld(room.gridPos);
            float d = Vector3.Distance(pos, worldPos);
            if (d < bestDist)
            {
                bestDist = d;
                closest = room;
            }
        }

        return closest;
    }
}

