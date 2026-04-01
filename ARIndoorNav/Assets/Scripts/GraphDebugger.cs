using UnityEngine;

public class GraphDebugger : MonoBehaviour
{
    public GraphManager graphManager;

    void OnDrawGizmos()
    {
        if (graphManager == null) return;
        if (DebugManager.Instance == null) return;

        var nodes = graphManager.GetAllNodes();

        foreach (var nodePair in nodes)
        {
            GraphNode node = nodePair.Value;

            // Draw nodes by type
            if (DebugManager.Instance.showNodes)
            {
                Gizmos.color = DebugColors.GetNodeTypeColor(node.Type);
                Gizmos.DrawSphere(node.Position, 0.2f);
                #if UNITY_EDITOR
                    UnityEditor.Handles.Label(node.Position + Vector3.up * 0.3f, node.Id);
                #endif

            }

            // Draw edges
            if (DebugManager.Instance.showEdges)
            {
                Gizmos.color = DebugColors.Edge;

                foreach (var edge in node.Neighbors)
                {
                    Gizmos.DrawLine(node.Position, edge.TargetNode.Position);
                }
            }
        }
    }
}