using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* pathfinding on the IndoorGraph.
/// Returns an ordered list of GraphNodes from startId to goalId.
/// Returns null if no path exists.
/// </summary>
public static class AStarRouter
{
    public static List<GraphNode> FindPath(IndoorGraph graph, string startId, string goalId)
    {
        if (graph == null)            { Debug.LogError("[AStarRouter] Graph is null.");  return null; }
        if (!graph.HasNode(startId))  { Debug.LogError($"[AStarRouter] Start node '{startId}' not found."); return null; }
        if (!graph.HasNode(goalId))   { Debug.LogError($"[AStarRouter] Goal node '{goalId}' not found.");  return null; }
        if (startId == goalId)        { return new List<GraphNode> { graph.GetNode(startId) }; }

        var openSet   = new HashSet<string> { startId };
        var cameFrom  = new Dictionary<string, string>();
        var gScore    = new Dictionary<string, float> { [startId] = 0f };
        var fScore    = new Dictionary<string, float> { [startId] = Heuristic(graph, startId, goalId) };

        while (openSet.Count > 0)
        {
            // Pick node in openSet with lowest fScore
            string current = null;
            float  bestF   = float.MaxValue;
            foreach (var id in openSet)
            {
                float f = fScore.ContainsKey(id) ? fScore[id] : float.MaxValue;
                if (f < bestF) { bestF = f; current = id; }
            }

            if (current == goalId)
                return ReconstructPath(graph, cameFrom, current);

            openSet.Remove(current);

            foreach (var neighborId in graph.GetNeighbors(current))
            {
                float tentativeG = (gScore.ContainsKey(current) ? gScore[current] : float.MaxValue)
                                 + EdgeCost(graph, current, neighborId);

                float neighborG = gScore.ContainsKey(neighborId) ? gScore[neighborId] : float.MaxValue;

                if (tentativeG < neighborG)
                {
                    cameFrom[neighborId] = current;
                    gScore[neighborId]   = tentativeG;
                    fScore[neighborId]   = tentativeG + Heuristic(graph, neighborId, goalId);
                    openSet.Add(neighborId);
                }
            }
        }

        Debug.LogWarning($"[AStarRouter] No path found from '{startId}' to '{goalId}'.");
        return null;
    }

    private static float Heuristic(IndoorGraph graph, string fromId, string toId)
    {
        GraphNode a = graph.GetNode(fromId);
        GraphNode b = graph.GetNode(toId);
        if (a == null || b == null) return 0f;
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static float EdgeCost(IndoorGraph graph, string fromId, string toId)
    {
        // Cost = Euclidean distance between nodes
        return Heuristic(graph, fromId, toId);
    }

    private static List<GraphNode> ReconstructPath(IndoorGraph graph, Dictionary<string, string> cameFrom, string current)
    {
        var path = new List<string> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }

        var result = new List<GraphNode>();
        foreach (var id in path)
            result.Add(graph.GetNode(id));
        return result;
    }
}
