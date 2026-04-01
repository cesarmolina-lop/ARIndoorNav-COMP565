using System.Collections.Generic;
using UnityEngine;

public enum NodeType
{
    Intersection,
    Hallway,
    Stairs,
    Elevator,
    Classroom,
    POI
}

// Represents a node in the indoor waypoint graph
public class GraphNode
{
    public string Id;               // Unique identifier
    public Vector3 Position;        // 3D world position relative to AR origin
    public int FloorLevel;          // Optional floor level
    public NodeType Type;           // Type of node (intersection, POI, etc.)

    // Connections to other nodes
    public List<GraphEdge> Neighbors;

    // A* pathfinding properties
    public GraphNode Parent;        // For reconstructing the path
    public float GCost;             // Cost from start node
    public float HCost;             // Heuristic cost to end node
    public float FCost => GCost + HCost;  // Total cost

    public GraphNode(string id, Vector3 position, int floor, NodeType type)
    {
        Id = id;
        Position = position;
        FloorLevel = floor;
        Type = type;
        Neighbors = new List<GraphEdge>();
    }
}

// Represents an edge connecting two nodes
public class GraphEdge
{
    public GraphNode TargetNode;    // The node this edge points to
    public float Cost;              // Traversal cost (distance, accessibility weighting, etc.)

    public GraphEdge(GraphNode target, float cost)
    {
        TargetNode = target;
        Cost = cost;
    }
}

// Simple A* pathfinding skeleton
public class AStar
{
    public static List<GraphNode> FindPath(GraphNode start, GraphNode goal)
    {
        var openSet = new List<GraphNode> { start };
        var closedSet = new HashSet<GraphNode>();

        start.GCost = 0;
        start.HCost = Vector3.Distance(start.Position, goal.Position);

        while (openSet.Count > 0)
        {
            // Get node with lowest F cost
            GraphNode current = openSet[0];
            foreach (var node in openSet)
            {
                if (node.FCost < current.FCost ||
                    (node.FCost == current.FCost && node.HCost < current.HCost))
                    current = node;
            }

            if (current == goal)
                return ReconstructPath(goal);

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (var edge in current.Neighbors)
            {
                GraphNode neighbor = edge.TargetNode;
                if (closedSet.Contains(neighbor))
                    continue;

                float tentativeG = current.GCost + edge.Cost;
                if (!openSet.Contains(neighbor))
                    openSet.Add(neighbor);
                else if (tentativeG >= neighbor.GCost)
                    continue;

                neighbor.Parent = current;
                neighbor.GCost = tentativeG;
                neighbor.HCost = Vector3.Distance(neighbor.Position, goal.Position);
            }
        }

        return null; // No path found
    }

    private static List<GraphNode> ReconstructPath(GraphNode endNode)
    {
        var path = new List<GraphNode>();
        GraphNode current = endNode;
        while (current != null)
        {
            path.Add(current);
            current = current.Parent;
        }
        path.Reverse();
        return path;
    }
}