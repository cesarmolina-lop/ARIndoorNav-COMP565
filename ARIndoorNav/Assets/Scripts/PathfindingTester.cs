using System.Collections.Generic;
using UnityEngine;

public class PathfindingTester : MonoBehaviour
{
    public GraphManager graphManager;
    public RouteDebugger routeDebugger;

    void Start()
    {
        List<GraphNode> path = graphManager.FindPath("C101", "C205");

        if (path == null)
        {
            Debug.Log("No path found");
            return;
        }

        Debug.Log("Path:");

        foreach (GraphNode node in path)
        {
            Debug.Log(node.Id);
        }

        // Send path to debugger
        routeDebugger.currentPath = path;
    }
}