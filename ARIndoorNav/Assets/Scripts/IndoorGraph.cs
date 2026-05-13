using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GraphNode
{
    public string id;
    public string label;
    public float  x;
    public float  z;
    public int    beaconMajor;   // -1 = no beacon
    public bool   isDestination; // true = show in destination picker
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

    private Dictionary<string, GraphNode>        _nodeMap;
    private Dictionary<string, List<string>>     _adjacency;

    public void Build()
    {
        _nodeMap   = new Dictionary<string, GraphNode>();
        _adjacency = new Dictionary<string, List<string>>();

        foreach (var node in nodes)
        {
            _nodeMap[node.id]   = node;
            _adjacency[node.id] = new List<string>();
        }

        foreach (var edge in edges)
        {
            if (_adjacency.ContainsKey(edge.from)) _adjacency[edge.from].Add(edge.to);
            if (_adjacency.ContainsKey(edge.to))   _adjacency[edge.to].Add(edge.from);
        }
    }

    public GraphNode       GetNode(string id)      { _nodeMap.TryGetValue(id, out var n); return n; }
    public List<string>    GetNeighbors(string id)  { _adjacency.TryGetValue(id, out var l); return l ?? new List<string>(); }
    public bool            HasNode(string id)        => _nodeMap != null && _nodeMap.ContainsKey(id);

    public List<GraphNode> GetDestinationNodes()
    {
        var result = new List<GraphNode>();
        foreach (var n in nodes) if (n.isDestination) result.Add(n);
        return result;
    }
}
