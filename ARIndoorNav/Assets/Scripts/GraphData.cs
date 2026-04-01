using System;
using System.Collections.Generic;

[Serializable]
public class GraphData
{
    public List<NodeData> nodes;
    public List<EdgeData> edges;
}

[Serializable]
public class NodeData
{
    public string id;
    public PositionData position;
    public int floor;
    public string type;
}

[Serializable]
public class PositionData
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class EdgeData
{
    public string from;
    public string to;
    public float cost;
}