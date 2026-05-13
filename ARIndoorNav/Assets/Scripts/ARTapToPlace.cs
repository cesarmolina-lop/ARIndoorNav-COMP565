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

        bool tappedThisFrame = false;
        Vector2 tapPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        var touchscreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            tappedThisFrame = true;
            tapPosition = touchscreen.primaryTouch.position.ReadValue();
        }
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            tappedThisFrame = true;
            tapPosition = mouse.position.ReadValue();
        }
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            tappedThisFrame = true;
            tapPosition     = Input.GetTouch(0).position;
        }
#endif

        if (!tappedThisFrame) return;

        if (raycastManager.Raycast(tapPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            if (spawnedObject == null)
                spawnedObject = Instantiate(placementPrefab, hitPose.position, hitPose.rotation);
            else
                spawnedObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);

            NavigationManager.Instance?.OnAnchorPlaced(spawnedObject.transform);

            if (lockAfterFirstPlacement)
                _anchorLocked = true;

            Debug.Log($"[ARTapToPlace] Anchor placed at {hitPose.position}");
        }
        else
        {
            Debug.Log("[ARTapToPlace] Tap did not hit a detected plane. Keep scanning...");
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