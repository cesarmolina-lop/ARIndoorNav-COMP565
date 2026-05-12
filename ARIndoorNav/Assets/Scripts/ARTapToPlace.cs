using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// ARTapToPlace — updated version.
/// Places the anchor on the first tap, then notifies NavigationManager.
/// Subsequent taps are ignored (anchor is locked after first placement).
/// To allow re-placement, call ResetAnchor() from your debug panel if needed.
/// </summary>
public class ARTapToPlace : MonoBehaviour
{
    [Header("Prefab to place on tap (your existing AnchorRoot prefab)")]
    public GameObject placementPrefab;

    [Header("Lock anchor after first placement (recommended for demo)")]
    public bool lockAfterFirstPlacement = true;

    private ARRaycastManager raycastManager;
    private GameObject spawnedObject;
    private bool _anchorLocked = false;

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
    }

    void Update()
    {
        if (_anchorLocked) return;
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began) return;

        if (raycastManager.Raycast(touch.position, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            if (spawnedObject == null)
            {
                spawnedObject = Instantiate(placementPrefab, hitPose.position, hitPose.rotation);
            }
            else
            {
                spawnedObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
            }

            // Notify NavigationManager that anchor is placed
            NavigationManager.Instance?.OnAnchorPlaced(spawnedObject.transform);

            if (lockAfterFirstPlacement)
                _anchorLocked = true;

            Debug.Log($"[ARTapToPlace] Anchor placed at {hitPose.position}");
        }
    }

    /// <summary>
    /// Call this to allow re-placement (e.g. from a "Recenter" button).
    /// </summary>
    public void ResetAnchor()
    {
        _anchorLocked = false;
        if (spawnedObject != null) Destroy(spawnedObject);
        spawnedObject = null;
        Debug.Log("[ARTapToPlace] Anchor reset. Tap floor to re-place.");
    }
}
