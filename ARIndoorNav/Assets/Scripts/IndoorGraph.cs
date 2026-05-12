using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data classes for deserializing indoor_graph.json.
/// Place indoor_graph.json in Assets/Resources/indoor_graph.json
/// Load with: Resources.Load<TextAsset>("indoor_graph")
/// </summary>

[System.Serializable]
public class GraphNode
{
    public string id;
    public string label;
    public float x;
    public float z;
    public int beaconMajor;       // -1 = no beacon at this node
    public bool isDestination;    // true = show in destination picker UI
}

[System.Serializable]
public class GraphEdge
{
    public string from;
    public string to;
}

[System.Serializable]
public class IndoorGraph
{
    public List<GraphNode> nodes;
    public List<GraphEdge> edges;

    // ── Runtime lookup helpers (not serialized) ────────────────────────────────

    private Dictionary<string, GraphNode> _nodeMap;
    private Dictionary<string, List<string>> _adjacency;

    /// <summary>Call after deserialization to build fast lookup tables.</summary>
    public void Build()
    {
        _nodeMap = new Dictionary<string, GraphNode>();
        _adjacency = new Dictionary<string, List<string>>();

        foreach (var node in nodes)
        {
            _nodeMap[node.id] = node;
            _adjacency[node.id] = new List<string>();
        }

        // Edges are undirected — add both directions
        foreach (var edge in edges)
        {
            if (_adjacency.ContainsKey(edge.from))
                _adjacency[edge.from].Add(edge.to);
            if (_adjacency.ContainsKey(edge.to))
                _adjacency[edge.to].Add(edge.from);
        }
    }

    public GraphNode GetNode(string id)
    {
        _nodeMap.TryGetValue(id, out GraphNode node);
        return node;
    }

    public List<string> GetNeighbors(string id)
    {
        _adjacency.TryGetValue(id, out List<string> neighbors);
        return neighbors ?? new List<string>();
    }

    public bool HasNode(string id) => _nodeMap != null && _nodeMap.ContainsKey(id);

    /// <summary>All nodes marked isDestination = true, for the UI picker.</summary>
    public List<GraphNode> GetDestinationNodes()
    {
        var result = new List<GraphNode>();
        foreach (var node in nodes)
            if (node.isDestination) result.Add(node);
        return result;
    }
}
