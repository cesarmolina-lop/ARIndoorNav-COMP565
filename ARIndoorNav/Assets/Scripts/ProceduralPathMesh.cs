using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralPathMesh : MonoBehaviour
{
    [Header("Path Settings")]
    [SerializeField] private float pathWidth = 0.3f;
    [SerializeField] private float yOffset = 0.02f;

    [Header("Test Points")]
    [SerializeField]
    private List<Vector3> testPoints = new List<Vector3>()
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(0f, 0f, 1f),
        new Vector3(1f, 0f, 2f),
        new Vector3(2f, 0f, 3f)
    };

    private MeshFilter meshFilter;
    private Mesh meshInstance;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        CreateMeshIfNeeded();
        GeneratePathMesh(testPoints);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            meshFilter = GetComponent<MeshFilter>();
            CreateMeshIfNeeded();
            GeneratePathMesh(testPoints);
        }
    }
#endif

    private void CreateMeshIfNeeded()
    {
        if (meshInstance == null)
        {
            meshInstance = new Mesh();
            meshInstance.name = "ProceduralPathMesh";
        }

        meshFilter.sharedMesh = meshInstance;
    }

    public void GeneratePathMesh(List<Vector3> points)
    {
        if (meshInstance == null)
        {
            CreateMeshIfNeeded();
        }

        meshInstance.Clear();

        if (points == null || points.Count < 2)
        {
            Debug.LogWarning("Need at least 2 points to generate a path mesh.");
            return;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float totalLength = 0f;
        List<float> segmentLengths = new List<float>();

        for (int i = 0; i < points.Count - 1; i++)
        {
            float length = Vector3.Distance(points[i], points[i + 1]);
            segmentLengths.Add(length);
            totalLength += length;
        }

        float accumulatedLength = 0f;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 currentPoint = points[i] + Vector3.up * yOffset;

            Vector3 forward;

            if (i == 0)
            {
                forward = (points[i + 1] - points[i]).normalized;
            }
            else if (i == points.Count - 1)
            {
                forward = (points[i] - points[i - 1]).normalized;
            }
            else
            {
                Vector3 dirA = (points[i] - points[i - 1]).normalized;
                Vector3 dirB = (points[i + 1] - points[i]).normalized;
                forward = (dirA + dirB).normalized;

                if (forward == Vector3.zero)
                {
                    forward = dirB;
                }
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float halfWidth = pathWidth * 0.5f;

            Vector3 leftVertex = currentPoint - right * halfWidth;
            Vector3 rightVertex = currentPoint + right * halfWidth;

            vertices.Add(leftVertex);
            vertices.Add(rightVertex);

            float v = (totalLength > 0f) ? accumulatedLength / totalLength : 0f;
            uvs.Add(new Vector2(0f, v));
            uvs.Add(new Vector2(1f, v));

            if (i < segmentLengths.Count)
            {
                accumulatedLength += segmentLengths[i];
            }
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            int vertIndex = i * 2;

            triangles.Add(vertIndex);
            triangles.Add(vertIndex + 2);
            triangles.Add(vertIndex + 1);

            triangles.Add(vertIndex + 1);
            triangles.Add(vertIndex + 2);
            triangles.Add(vertIndex + 3);
        }

        meshInstance.SetVertices(vertices);
        meshInstance.SetTriangles(triangles, 0);
        meshInstance.SetUVs(0, uvs);
        meshInstance.RecalculateNormals();
        meshInstance.RecalculateBounds();
    }

    public void SetPath(List<Vector3> newPoints)
    {
        testPoints = newPoints;
        GeneratePathMesh(testPoints);
    }

    public List<Vector3> GetTestPoints()
    {
        return testPoints;
    }
}