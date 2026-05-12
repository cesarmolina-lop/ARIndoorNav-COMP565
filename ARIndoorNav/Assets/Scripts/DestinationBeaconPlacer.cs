using System.Collections.Generic;
using UnityEngine;

public class DestinationBeaconPlacer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ProceduralPathMesh pathMesh;
    [SerializeField] private GameObject destinationBeaconPrefab;

    [Header("Placement")]
    [SerializeField] private float beaconYOffset = 0.25f;
    [SerializeField] private bool generateOnStart = true;

    private GameObject currentBeacon;

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateDestinationBeacon();
        }
    }

    public void GenerateDestinationBeacon()
    {
        ClearBeacon();

        if (pathMesh == null)
        {
            Debug.LogWarning("DestinationBeaconPlacer: PathMesh reference is missing.");
            return;
        }

        if (destinationBeaconPrefab == null)
        {
            Debug.LogWarning("DestinationBeaconPlacer: Destination beacon prefab is missing.");
            return;
        }

        List<Vector3> points = pathMesh.GetTestPoints();

        if (points == null || points.Count == 0)
        {
            Debug.LogWarning("DestinationBeaconPlacer: No path points available.");
            return;
        }

        Vector3 finalPoint = points[points.Count - 1];
        Vector3 spawnPosition = finalPoint + Vector3.up * beaconYOffset;

        currentBeacon = Instantiate(destinationBeaconPrefab, spawnPosition, Quaternion.identity, transform);
        currentBeacon.name = "DestinationBeacon";
    }

    public void ClearBeacon()
    {
        if (currentBeacon != null)
        {
            Destroy(currentBeacon);
            currentBeacon = null;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }
}