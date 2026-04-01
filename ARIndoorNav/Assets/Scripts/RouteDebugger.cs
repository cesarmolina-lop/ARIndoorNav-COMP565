using System.Collections.Generic;
using UnityEngine;

public class RouteDebugger : MonoBehaviour
{
    public List<GraphNode> currentPath;

    void OnDrawGizmos()
    {
        if (DebugManager.Instance == null) return;
        if (!DebugManager.Instance.showPath) return;
        if (currentPath == null || currentPath.Count < 2) return;

        // Draw path lines
        Gizmos.color = DebugColors.Path;

        for (int i = 0; i < currentPath.Count - 1; i++)
        {
            Gizmos.DrawLine(
                currentPath[i].Position,
                currentPath[i + 1].Position
            );
        }

        // Start node
        Gizmos.color = DebugColors.StartNode;
        Gizmos.DrawSphere(currentPath[0].Position, 0.3f);

        // Goal node
        Gizmos.color = DebugColors.GoalNode;
        Gizmos.DrawSphere(currentPath[currentPath.Count - 1].Position, 0.3f);
    }
}