using UnityEngine;

public class DebugManager : MonoBehaviour
{
    public static DebugManager Instance;

    public bool showNodes = true;
    public bool showEdges = true;
    public bool showPath = true;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            showNodes = !showNodes;
            showEdges = showNodes;
            showPath = showNodes;

            Debug.Log("Debug Mode: " + showNodes);
        }
    }
}