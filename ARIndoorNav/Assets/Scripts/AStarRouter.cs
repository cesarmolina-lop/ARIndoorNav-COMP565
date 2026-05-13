using System.Collections.Generic;
using UnityEngine;

public static class AStarRouter
{
    public static List<GraphNode> FindPath(IndoorGraph graph, string startId, string goalId)
    {
        if (graph == null)           { Debug.LogError("[AStarRouter] Graph is null.");                      return null; }
        if (!graph.HasNode(startId)) { Debug.LogError($"[AStarRouter] Start '{startId}' not in graph.");   return null; }
        if (!graph.HasNode(goalId))  { Debug.LogError($"[AStarRouter] Goal '{goalId}' not in graph.");     return null; }
        if (startId == goalId)       { return new List<GraphNode> { graph.GetNode(startId) }; }

        var openSet  = new HashSet<string> { startId };
        var cameFrom = new Dictionary<string, string>();
        var gScore   = new Dictionary<string, float> { [startId] = 0f };
        var fScore   = new Dictionary<string, float> { [startId] = H(graph, startId, goalId) };

        while (openSet.Count > 0)
        {
            string current = null;
            float  bestF   = float.MaxValue;
            foreach (var id in openSet) { float f = fScore.ContainsKey(id) ? fScore[id] : float.MaxValue; if (f < bestF) { bestF = f; current = id; } }

            if (current == goalId) return Reconstruct(graph, cameFrom, current);

            openSet.Remove(current);

            foreach (var nb in graph.GetNeighbors(current))
            {
                float tg = (gScore.ContainsKey(current) ? gScore[current] : float.MaxValue) + H(graph, current, nb);
                if (tg < (gScore.ContainsKey(nb) ? gScore[nb] : float.MaxValue))
                {
                    cameFrom[nb] = current;
                    gScore[nb]   = tg;
                    fScore[nb]   = tg + H(graph, nb, goalId);
                    openSet.Add(nb);
                }
            }
        }

        Debug.LogWarning($"[AStarRouter] No path: '{startId}' → '{goalId}'.");
        return null;
    }

    static float H(IndoorGraph g, string a, string b)
    {
        var na = g.GetNode(a); var nb = g.GetNode(b);
        if (na == null || nb == null) return 0f;
        float dx = na.x - nb.x, dz = na.z - nb.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static List<GraphNode> Reconstruct(IndoorGraph g, Dictionary<string, string> came, string current)
    {
        var path = new List<string> { current };
        while (came.ContainsKey(current)) { current = came[current]; path.Insert(0, current); }
        var result = new List<GraphNode>();
        foreach (var id in path) result.Add(g.GetNode(id));
        return result;
    }
}
