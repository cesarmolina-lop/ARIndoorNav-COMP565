using UnityEngine;

public static class DebugColors
{
    public static Color Node = Color.blue;
    public static Color Edge = Color.white;
    public static Color Path = Color.green;
    public static Color StartNode = Color.yellow;
    public static Color GoalNode = Color.magenta;
    public static Color CurrentNode = Color.red;

    public static Color GetNodeTypeColor(NodeType type)
    {
        switch (type)
        {
            case NodeType.Intersection:
                return Color.cyan;

            case NodeType.Hallway:
                return Color.blue;

            case NodeType.Stairs:
                return Color.red;

            case NodeType.Elevator:
                return Color.magenta;

            case NodeType.Classroom:
                return Color.yellow;

            case NodeType.POI:
                return Color.green;

            default:
                return Color.white;
        }
    }
}