using System.Collections.Generic;
using UnityEngine;

public class GraphManager : MonoBehaviour
{
    public TextAsset graphJson;

    private Dictionary<string, GraphNode> nodes = new Dictionary<string, GraphNode>();

    void Awake()
    {
        LoadGraph();
    }

    void LoadGraph()
    {
        GraphData data = JsonUtility.FromJson<GraphData>(graphJson.text);

        // Create nodes
        foreach (var nodeData in data.nodes)
        {
            Vector3 pos = new Vector3(
                nodeData.position.x,
                nodeData.position.y,
                nodeData.position.z
            );

            NodeType type = ParseNodeType(nodeData.type);

            GraphNode node = new GraphNode(
                nodeData.id,
                pos,
                nodeData.floor,
                type
            );

            nodes[node.Id] = node;
        }

        // Create edges
        foreach (var edgeData in data.edges)
        {
            GraphNode from = nodes[edgeData.from];
            GraphNode to = nodes[edgeData.to];

            from.Neighbors.Add(new GraphEdge(to, edgeData.cost));
            to.Neighbors.Add(new GraphEdge(from, edgeData.cost)); // bidirectional
        }

        Debug.Log("Graph Loaded: " + nodes.Count + " nodes");
    }

    NodeType ParseNodeType(string type)
    {
        switch (type.ToLower())
        {
            case "intersection": return NodeType.Intersection;
            case "hallway": return NodeType.Hallway;
            case "stairs": return NodeType.Stairs;
            case "elevator": return NodeType.Elevator;
            case "classroom": return NodeType.Classroom;
            case "poi": return NodeType.POI;
            default: return NodeType.Intersection;
        }
    }

    public GraphNode GetNode(string id)
    {
        return nodes.ContainsKey(id) ? nodes[id] : null;
    }

    public List<GraphNode> FindPath(string startId, string goalId)
    {
        GraphNode start = GetNode(startId);
        GraphNode goal = GetNode(goalId);

        if (start == null || goal == null)
        {
            Debug.LogError("Invalid node ID");
            return null;
        }

        return AStar.FindPath(start, goal);
    }

    public Dictionary<string, GraphNode> GetAllNodes()
    {
        return nodes;
    }
}