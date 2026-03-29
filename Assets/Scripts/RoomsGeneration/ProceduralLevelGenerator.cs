using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class ProceduralLevelGenerator : MonoBehaviour
{
    public enum RoomKind
    {
        Entrance,
        Normal,
        EnemySpawn,
        Loot,
        Objective
    }

    [Serializable]
    public class RoomNode
    {
        public Vector2Int gridPos;
        public RoomKind kind = RoomKind.Normal;
        public List<RoomNode> neighbors = new List<RoomNode>();

        [NonSerialized] public GameObject instance;
    }

    [Header("Layout Settings")]
    [Min(2)]
    public int mainPathLength = 10;

    public int cellSize = 10;          
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
    public GameObject doorPrefab;
    public GameObject wallsAroundDoor;
    public GameObject enemyPrefab;
    public GameObject workbenchPrefab;
    public GameObject craftItemsPrefab;
    public GameObject finishPrefab;

    [Header("Debug")]
    public bool regenerateOnStart = true;
    public bool drawGizmos = true;

    [Header("Connection Markers")]
    public GameObject doorwayMarkerPrefab;
    public float doorwayMarkerHeight = 1.0f;
    public float doorwayMarkerScale = 1.0f;

    [Header("Room Markers")]
    public GameObject roomMarkerPrefab;        
    public float markerFloorOffset = 0.05f;    
    public float markerWallInset = 1.0f;      
    public float markerFloorPadding = 1.0f;    

    [Header("Enemy Spawn Rooms")]
    [Range(0f, 1f)]
    public float enemySpawnRoomChance = 0.25f;
    [Min(0)]
    public int maxEnemySpawnRooms = 4;

    [Header("Loot Rooms")]
    [Range(0f, 1f)]
    public float lootRoomChance = 0.2f;

    [SerializeField]
    private List<RoomNode> rooms = new List<RoomNode>();

    // Markers List
    private readonly List<GameObject> spawnedDoorwayMarkers = new List<GameObject>();

    // List of all doors spawned
    private readonly List<GameObject> spawnedDoors = new List<GameObject>();

    // For quick overlap checks
    private Dictionary<Vector2Int, RoomNode> roomLookup = new Dictionary<Vector2Int, RoomNode>();

    // Track spawned room instances so we can clean them up
    private readonly List<GameObject> spawnedInstances = new List<GameObject>();

    private readonly Dictionary<Vector2Int, GameObject> enemyByRoom = new Dictionary<Vector2Int, GameObject>();

    //Spawn objects markers
    private readonly List<GameObject> spawnedRoomMarkers = new List<GameObject>();



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

            RoomKind kind = (rng.NextDouble() < lootRoomChance)
                ? RoomKind.Loot
                : RoomKind.Normal;

            RoomNode newRoom = CreateRoom(nextPos, kind);

            // Link neighbors both ways
            lastRoom.neighbors.Add(newRoom);
            newRoom.neighbors.Add(lastRoom);
            EnsureDoorMarker(newRoom, lastRoom);

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

        // 5. Assign enemy spawns
        AssignEnemySpawnRooms(rng);

        Debug.Log($"Generated layout with {rooms.Count} rooms " +
                  $"({mainPathCount} on main path, branches added).");

        // 6. Build actual room instances in the scene
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

    //Branches generation
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

            RoomKind kind = (rng.NextDouble() < lootRoomChance)
                ? RoomKind.Loot
                : RoomKind.Normal;

            RoomNode newRoom = CreateRoom(nextPos, kind);

            // Link both ways
            previous.neighbors.Add(newRoom);
            newRoom.neighbors.Add(previous);
            EnsureDoorMarker(newRoom, previous);

            previous = newRoom;
            currentPos = nextPos;
        }

        branchesCreated++;
    }

    // Assign some rooms as enemy spawners
    private void AssignEnemySpawnRooms(System.Random rng)
    {
        if (maxEnemySpawnRooms <= 0) return;

        // Collect candidates: ONLY normal rooms 
        List<RoomNode> candidates = new List<RoomNode>();
        foreach (var room in rooms)
        {
            if (room.kind == RoomKind.Normal)
                candidates.Add(room);
        }

        // Shuffle candidates (Fisher-Yates) so selection is random but deterministic with seed
        for (int i = 0; i < candidates.Count - 1; i++)
        {
            int j = rng.Next(i, candidates.Count);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int made = 0;

        // Walk the shuffled list and upgrade some rooms based on chance, until max reached
        foreach (var room in candidates)
        {
            if (made >= maxEnemySpawnRooms) break;

            if (rng.NextDouble() <= enemySpawnRoomChance)
            {
                room.kind = RoomKind.EnemySpawn;
                made++;
            }
        }
    }

    //Create door markers
    private void EnsureDoorMarker(RoomNode r1, RoomNode r2)
    {
        if (doorwayMarkerPrefab == null) return;

        Vector3 w1 = GridToWorld(r1.gridPos);
        Vector3 w2 = GridToWorld(r2.gridPos);

        Vector3 mid = (w1 + w2) * 0.5f;
        mid.y += doorwayMarkerHeight;

        Vector3 dir = (w2 - w1);
        dir.y = 0f;
        Quaternion rot = (dir.sqrMagnitude > 0.0001f)
            ? Quaternion.LookRotation(dir.normalized, Vector3.up)
            : Quaternion.identity;

        Transform parent = levelRoot != null ? levelRoot : transform;
        GameObject marker = Instantiate(doorwayMarkerPrefab, mid, rot, parent);
        marker.transform.localScale = Vector3.one * doorwayMarkerScale;

        spawnedDoorwayMarkers.Add(marker);
    }

        // ---------- GEOMETRY LAYER ----------

    private void ClearGeometry()
    {
        // Clear previously spawned instances
        foreach (var obj in spawnedInstances)
        {
            if (obj == null) continue;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        spawnedInstances.Clear();

        // Clear doorways
        foreach (var obj in spawnedDoorwayMarkers)
        {
            if (obj == null) continue;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }
        spawnedDoorwayMarkers.Clear();

        //Clear doors
        foreach (var d in spawnedDoors)
        {
            if (d == null) continue;

            if (Application.isPlaying)
                Destroy(d);
            else
                DestroyImmediate(d);
        }
        spawnedDoors.Clear();

        // Clear children under levelRoot
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

        // Clear enemies
        foreach (var kvp in enemyByRoom)
        {
            if (kvp.Value == null) continue;

            if (Application.isPlaying) Destroy(kvp.Value);
            else DestroyImmediate(kvp.Value);
        }
        enemyByRoom.Clear();

        // Clear room markers
        foreach (var m in spawnedRoomMarkers)
        {
            if (m == null) continue;

            if (Application.isPlaying)
                Destroy(m);
            else
                DestroyImmediate(m);
        }
        spawnedRoomMarkers.Clear();
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

            // -------------------------------
            // CREATE DOORWAYS
            // -------------------------------

            foreach (var marker in spawnedDoorwayMarkers)
            {
                if (marker == null) continue;
                if (marker.transform.childCount > 0) continue; // door already spawned as child

                var door = Instantiate(doorPrefab, marker.transform.position, marker.transform.rotation, marker.transform);
                door.transform.localPosition += Vector3.up * 0f;
                spawnedDoors.Add(door);

                var wallDoor = Instantiate(wallsAroundDoor, marker.transform.position, marker.transform.rotation, marker.transform);
                wallDoor.transform.localPosition += Vector3.up * 0f;
                spawnedInstances.Add(wallDoor);
            }

            // -------------------------------
            // SPAWN ENEMIES
            // -------------------------------

            SpawnEnemies();
        }

        // -------------------------------
        // SPAWN WORCKBENCH
        // -------------------------------

        RoomNode entrance = null;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].kind == RoomKind.Entrance)
            {
                entrance = rooms[i];
                break;
            }
        }

        GameObject entranceMarker = CreateMarkerNearEmptyWall(entrance);

        if (entranceMarker != null && workbenchPrefab != null)
        {
            Instantiate(workbenchPrefab, entranceMarker.transform.position, entranceMarker.transform.rotation, levelRoot);
        }

        // -------------------------------
        // SPAWN CRAFTABLES
        // -------------------------------

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].kind == RoomKind.Loot)
            {
                var lootMarkers = CreateRandomFloorMarkers(rooms[i], 3, seedOffset: 9000);

                foreach (var m in lootMarkers) 
                {
                    Instantiate(craftItemsPrefab, m.transform.position, Quaternion.identity, levelRoot);

                }
            }
        }

        // -------------------------------
        // SPAWN FINISH
        // -------------------------------

        RoomNode objective = null;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].kind == RoomKind.Objective)
            {
                objective = rooms[i];
                break;
            }
        }

        GameObject objectiveMarker = CreateMarkerNearEmptyWall(objective);

        if (objectiveMarker != null && finishPrefab != null)
        {
            Instantiate(finishPrefab, objectiveMarker.transform.position, objectiveMarker.transform.rotation, levelRoot);
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

    //Spawn enemies in rooms
    private void SpawnEnemies()
    {
        if (enemyPrefab == null)
            return;

        Transform parent = levelRoot != null ? levelRoot : transform;

        foreach (var room in rooms)
        {
            if (room.kind != RoomKind.EnemySpawn)
                continue;

            if (room.instance == null)
                continue;

            // If there's already an enemy for this room, do nothing
            if (enemyByRoom.ContainsKey(room.gridPos))
                continue;

            Vector3 pos = room.instance.transform.position;
            pos.y = 0f;

            GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity, parent);
            enemyByRoom.Add(room.gridPos, enemy);
        }
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

    private float GetRoomFloorTopY(RoomNode room)
    {
        if (room == null || room.instance == null) return 0f;

        return room.instance.transform.position.y + 0.5f;
    }

    private GameObject CreateRoomMarker(RoomNode room, Vector3 worldPos, Quaternion rot)
    {
        if (roomMarkerPrefab == null) return null;
        if (room == null) return null;

        Transform parent = levelRoot != null ? levelRoot : transform;

        worldPos.y = GetRoomFloorTopY(room) + markerFloorOffset;

        GameObject marker = Instantiate(roomMarkerPrefab, worldPos, rot, parent);
        spawnedRoomMarkers.Add(marker);
        return marker;
    }

    private GameObject CreateMarkerNearEmptyWall(RoomNode room)
    {
        if (room == null || room.instance == null) return null;

        Quaternion markerYawFix = Quaternion.Euler(0f, 180f, 0f);

        Vector3 center = room.instance.transform.position;
        float half = cellSize / 2f;

        // North (+Z)
        if (!room.neighbors.Exists(r => r.gridPos == room.gridPos + Vector2Int.up))
        {
            Vector3 pos = center + Vector3.forward * (half - markerWallInset);
            Quaternion rot = Quaternion.LookRotation(Vector3.back, Vector3.up) * markerYawFix; // face inward
            return CreateRoomMarker(room, pos, rot);
        }

        // East (+X)
        if (!room.neighbors.Exists(r => r.gridPos == room.gridPos + Vector2Int.right))
        {
            Vector3 pos = center + Vector3.right * (half - markerWallInset);
            Quaternion rot = Quaternion.LookRotation(Vector3.left, Vector3.up) * markerYawFix;
            return CreateRoomMarker(room, pos, rot);
        }

        // South (-Z)
        if (!room.neighbors.Exists(r => r.gridPos == room.gridPos + Vector2Int.down))
        {
            Vector3 pos = center + Vector3.back * (half - markerWallInset);
            Quaternion rot = Quaternion.LookRotation(Vector3.forward, Vector3.up) * markerYawFix;
            return CreateRoomMarker(room, pos, rot);
        }

        // West (-X)
        if (!room.neighbors.Exists(r => r.gridPos == room.gridPos + Vector2Int.left))
        {
            Vector3 pos = center + Vector3.left * (half - markerWallInset);
            Quaternion rot = Quaternion.LookRotation(Vector3.right, Vector3.up) * markerYawFix;
            return CreateRoomMarker(room, pos, rot);
        }

        // No empty wall found
        return null;
    }

    private List<GameObject> CreateRandomFloorMarkers(RoomNode room, int count, int seedOffset = 0)
    {
        List<GameObject> markers = new List<GameObject>();
        if (room == null || room.instance == null) return markers;
        if (roomMarkerPrefab == null) return markers;
        if (count <= 0) return markers;

        int seed = useRandomSeed ? Environment.TickCount : fixedSeed;
        System.Random rng = new System.Random(seed + seedOffset + room.gridPos.GetHashCode());

        Vector3 center = room.instance.transform.position;
        float half = cellSize / 2f;

        float minX = -half + markerFloorPadding;
        float maxX = half - markerFloorPadding;
        float minZ = -half + markerFloorPadding;
        float maxZ = half - markerFloorPadding;

        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
            float z = Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble());

            Vector3 pos = center + new Vector3(x, 0f, z);
            GameObject m = CreateRoomMarker(room, pos, Quaternion.identity);
            if (m != null) markers.Add(m);
        }

        return markers;
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
                case RoomKind.EnemySpawn:
                    Gizmos.color = Color.purple;
                    break;
                case RoomKind.Loot:
                    Gizmos.color = Color.yellow;
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

    public Vector3 GridToWorldPublic(Vector2Int gridPos)
    {
        return new Vector3(gridPos.x * cellSize, 0f, gridPos.y * cellSize);
    }

}

