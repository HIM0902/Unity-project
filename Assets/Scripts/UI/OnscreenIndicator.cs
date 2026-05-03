using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnscreenIndicator : MonoBehaviour
{
    [SerializeField] Transform target;               // Target object
    [SerializeField] RectTransform canvasTransform;  // Canvas for indicators
    [SerializeField] RectTransform indicatorPrefab;  // Down (on top) indicator prefab
    [SerializeField] RectTransform indicatorLeftPrefab;   // Left indicator prefab
    [SerializeField] RectTransform indicatorRightPrefab;  // Right indicator prefab

    [SerializeField] float edgePadding = 40f;   //Offset from the edge to place the indicator
    [SerializeField] Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);   // Offset above the target

    RectTransform currentIndicator; // Indicator RecTransform
    Camera mainCam; // Camera object

    bool showIndicator = false;  // Toggle when we are showing/tracking an indicator (button click)
    public bool IsShowing => showIndicator;
    public Transform CurrentTarget => target;

    void Start()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        if (!showIndicator) return;

        if (target == null || canvasTransform == null || mainCam == null)
        {
            Debug.LogWarning("Missing references on OnscreenIndicator.");
            return;
        }

        // World to screen position
        Vector3 screenPos = mainCam.WorldToScreenPoint(target.position + worldOffset);

        // We check if the point within the camera view
        bool onScreen =
            screenPos.z > 0f &&
            screenPos.x >= 0f && screenPos.x <= Screen.width &&
            screenPos.y >= 0f && screenPos.y <= Screen.height;

        if (onScreen)
        {
            // Target visible, we use down-arrow indicator placed on top of the object
            EnsureIndicator(indicatorPrefab);
            currentIndicator.position = new Vector3(screenPos.x, screenPos.y, 0f);
            return;
        }

        // Target not visible, we choose left or right indicator
        // Use dot with transform.right to find if the target is to our left or right
        Vector3 toTarget = target.position - transform.position;
        float rightDot = Vector3.Dot(transform.right, toTarget);

        // Clamp Y to edges so the arrow stays visible
        float clampedY = Mathf.Clamp(screenPos.y, edgePadding, Screen.height - edgePadding);

        if (rightDot < 0f)
        {
            // Object is offscreen to the left
            EnsureIndicator(indicatorLeftPrefab);
            currentIndicator.position = new Vector3(edgePadding, clampedY, 0f);
        }
        else
        {
            // Object is offscreen to the right
            EnsureIndicator(indicatorRightPrefab);
            currentIndicator.position = new Vector3(Screen.width - edgePadding, clampedY, 0f);
        }
    }

    // On-Click function to enable/disable indicator
    public void ToggleIndicators()
    {
        showIndicator = !showIndicator;

        if (!showIndicator)
        {
            HideIndicator();
        }
    }

    // Check to make sure prefab(s) are assigned
    void EnsureIndicator(RectTransform prefabToUse)
    {
        if (prefabToUse == null)
        {
            Debug.LogWarning("Indicator prefab is missing.");
            return;
        }

        // We name instances as: "<PrefabName>_Instance"
        string expectedName = prefabToUse.name + "_Instance";

        // If we already have the correct indicator, keep it
        if (currentIndicator != null && currentIndicator.name == expectedName)
            return;

        HideIndicator();

        // Instantiate + parent under canvas, worldPositionStays false
        currentIndicator = Instantiate(prefabToUse);
        currentIndicator.transform.SetParent(canvasTransform, worldPositionStays: false);
        currentIndicator.name = expectedName;
    }

    void HideIndicator()
    {
        if (currentIndicator != null)
        {
            Destroy(currentIndicator.gameObject);
            currentIndicator = null;
        }
    }

    // Turn indicator on and point it at a target
    public void ShowForTarget(Transform newTarget)
    {
        target = newTarget;
        showIndicator = true;
    }

    // Turn indicator off
    public void Hide()
    {
        showIndicator = false;
        HideIndicator();
    }
}