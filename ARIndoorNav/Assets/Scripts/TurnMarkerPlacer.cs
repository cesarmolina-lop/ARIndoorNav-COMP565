using System.Collections.Generic;
using UnityEngine;

public class TurnMarkerPlacer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ProceduralPathMesh pathMesh;
    [SerializeField] private GameObject turnMarkerPrefab;

    [Header("Turn Detection")]
    [SerializeField] private float turnAngleThreshold = 25f;
    [SerializeField] private float markerYOffset = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool generateOnStart = true;

    private readonly List<GameObject> spawnedMarkers = new List<GameObject>();

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateTurnMarkers();
        }
    }

/*
 #if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (pathMesh != null && turnMarkerPrefab != null)
            {
                GenerateTurnMarkersEditorSafe();
            }
        }
    }
#endif
*/
    public void GenerateTurnMarkers()
    {
        ClearMarkers();

        if (pathMesh == null)
        {
            Debug.LogWarning("TurnMarkerPlacer: PathMesh reference is missing.");
            return;
        }

        if (turnMarkerPrefab == null)
        {
            Debug.LogWarning("TurnMarkerPlacer: Turn marker prefab is missing.");
            return;
        }

        List<Vector3> points = pathMesh.GetTestPoints();

        if (points == null || points.Count < 3)
        {
            Debug.LogWarning("TurnMarkerPlacer: Need at least 3 points to detect turns.");
            return;
        }

        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector3 prev = points[i - 1];
            Vector3 current = points[i];
            Vector3 next = points[i + 1];

            Vector3 dirA = (current - prev).normalized;
            Vector3 dirB = (next - current).normalized;

            float angle = Vector3.Angle(dirA, dirB);

            if (angle >= turnAngleThreshold)
            {
                Vector3 spawnPosition = current + Vector3.up * markerYOffset;
                GameObject marker = Instantiate(turnMarkerPrefab, spawnPosition, Quaternion.identity, transform);
                spawnedMarkers.Add(marker);
            }
        }
    }

    /*
#if UNITY_EDITOR
    private void GenerateTurnMarkersEditorSafe()
    {
        ClearMarkersImmediate();

        List<Vector3> points = pathMesh.GetTestPoints();

        if (points == null || points.Count < 3)
        {
            return;
        }

        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector3 prev = points[i - 1];
            Vector3 current = points[i];
            Vector3 next = points[i + 1];

            Vector3 dirA = (current - prev).normalized;
            Vector3 dirB = (next - current).normalized;

            float angle = Vector3.Angle(dirA, dirB);

            if (angle >= turnAngleThreshold)
            {
                Vector3 spawnPosition = current + Vector3.up * markerYOffset;
                GameObject marker = Instantiate(turnMarkerPrefab, spawnPosition, Quaternion.identity, transform);
                marker.name = "TurnMarker";
                spawnedMarkers.Add(marker);
            }
        }
    }
#endif

*/

    public void ClearMarkers()
    {
        for (int i = spawnedMarkers.Count - 1; i >= 0; i--)
        {
            if (spawnedMarkers[i] != null)
            {
                Destroy(spawnedMarkers[i]);
            }
        }

        spawnedMarkers.Clear();
    }

    /*
    private void ClearMarkersImmediate()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            DestroyImmediate(transform.GetChild(i).gameObject);
#else
            Destroy(transform.GetChild(i).gameObject);
#endif
        }

        spawnedMarkers.Clear();
    }
    */
}