using System.Collections.Generic;
using UnityEngine;

public static class RoomPathfinder
{
    public static List<ProceduralLevelGenerator.RoomNode> FindPath(
        ProceduralLevelGenerator.RoomNode start,
        ProceduralLevelGenerator.RoomNode goal)
    {
        var open = new List<ProceduralLevelGenerator.RoomNode>();
        var closed = new HashSet<ProceduralLevelGenerator.RoomNode>();

        var cameFrom = new Dictionary<ProceduralLevelGenerator.RoomNode, ProceduralLevelGenerator.RoomNode>();
        var gScore = new Dictionary<ProceduralLevelGenerator.RoomNode, float>();
        var fScore = new Dictionary<ProceduralLevelGenerator.RoomNode, float>();

        open.Add(start);
        gScore[start] = 0;
        fScore[start] = Heuristic(start, goal);

        while (open.Count > 0)
        {
            var current = open[0];
            foreach (var n in open)
                if (fScore.ContainsKey(n) && fScore[n] < fScore[current])
                    current = n;

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            open.Remove(current);
            closed.Add(current);

            foreach (var neighbor in current.neighbors)
            {
                if (closed.Contains(neighbor)) continue;

                float tentativeG = gScore[current] + 1;

                if (!open.Contains(neighbor))
                    open.Add(neighbor);
                else if (tentativeG >= gScore.GetValueOrDefault(neighbor, float.MaxValue))
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                fScore[neighbor] = tentativeG + Heuristic(neighbor, goal);
            }
        }

        return null;
    }

    private static float Heuristic(ProceduralLevelGenerator.RoomNode a, ProceduralLevelGenerator.RoomNode b)
    {
        return Vector2Int.Distance(a.gridPos, b.gridPos);
    }

    private static List<ProceduralLevelGenerator.RoomNode> ReconstructPath(
        Dictionary<ProceduralLevelGenerator.RoomNode, ProceduralLevelGenerator.RoomNode> cameFrom,
        ProceduralLevelGenerator.RoomNode current)
    {
        var path = new List<ProceduralLevelGenerator.RoomNode> { current };

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
