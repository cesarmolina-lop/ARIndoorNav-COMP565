using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Places the AR anchor on first floor tap. Notifies NavigationManager.
/// Locks after first placement. Call ResetAnchor() to allow re-placement.
/// Supports both old and new Unity Input System.
/// </summary>
public class ARTapToPlace : MonoBehaviour
{
    [Header("Prefab placed on floor tap (your AnchorRoot or empty GO)")]
    public GameObject placementPrefab;

    [Header("Lock anchor after first tap (recommended for demo)")]
    public bool lockAfterFirstPlacement = true;

    private ARRaycastManager        raycastManager;
    private GameObject              spawnedObject;
    private bool                    _locked = false;
    static  List<ARRaycastHit>      hits    = new List<ARRaycastHit>();

    void Awake() => raycastManager = GetComponent<ARRaycastManager>();

    void Update()
    {
        if (_locked) return;

        bool    tapped   = false;
        Vector2 tapPos   = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        var ts = UnityEngine.InputSystem.Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
        { tapped = true; tapPos = ts.primaryTouch.position.ReadValue(); }

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        { tapped = true; tapPos = mouse.position.ReadValue(); }
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        { tapped = true; tapPos = Input.GetTouch(0).position; }
#endif

        if (!tapped) return;

        if (raycastManager.Raycast(tapPos, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            if (spawnedObject == null)
                spawnedObject = Instantiate(placementPrefab, hitPose.position, hitPose.rotation);
            else
                spawnedObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);

            NavigationManager.Instance?.OnAnchorPlaced(spawnedObject.transform);

            if (lockAfterFirstPlacement) _locked = true;

            Debug.Log($"[ARTapToPlace] Anchor placed at {hitPose.position}");
        }
        else
        {
            Debug.Log("[ARTapToPlace] Tap missed plane — keep scanning.");
        }
    }

    public void ResetAnchor()
    {
        _locked = false;
        if (spawnedObject) Destroy(spawnedObject);
        spawnedObject = null;
        Debug.Log("[ARTapToPlace] Anchor reset.");
    }
}
