using UnityEngine;

/// <summary>
/// Procedural reload animation — no animation clips needed.
/// Tilts the gun downward and slightly back during reload, then smoothly
/// returns to its original pose. Runs on a coroutine that the Gun script triggers.
///
/// SETUP:
///   1. Attach this to your gun GameObject (same one as the Gun script).
///   2. The Gun script will auto-find this and call PlayReload() during reload.
///   3. Tune the rotation/position offsets in the inspector to taste.
/// </summary>
public class GunReloadAnimator : MonoBehaviour
{
    [Header("Rotation Offset During Reload")]
    [Tooltip("How much the gun tilts down (X), sideways (Y), and rolls (Z) during reload.")]
    public Vector3 reloadRotationOffset = new Vector3(45f, 0f, 0f);

    [Header("Position Offset During Reload")]
    [Tooltip("How much the gun moves during reload. Negative Y pulls it down toward the hip.")]
    public Vector3 reloadPositionOffset = new Vector3(0f, -0.15f, -0.05f);

    [Header("Timing")]
    [Tooltip("Fraction of total reload time spent tilting DOWN. 0.3 = 30% of reload time. Rest is spent at bottom + returning.")]
    [Range(0.1f, 0.5f)]
    public float tiltDownFraction = 0.3f;

    [Tooltip("Fraction of total reload time spent returning UP at the end. 0.3 = last 30% spent returning.")]
    [Range(0.1f, 0.5f)]
    public float tiltUpFraction = 0.3f;

    [Header("Smoothing")]
    [Tooltip("Curve for tilting down. Default ease-out feels natural.")]
    public AnimationCurve tiltDownCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Curve for returning up. Default ease-in-out.")]
    public AnimationCurve tiltUpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Original pose, captured on Awake so we know where to return to
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    private Coroutine activeReload;

    private void Awake()
    {
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
    }

    /// <summary>
    /// Called by Gun.cs when reload starts.
    /// totalDuration should match weaponData.reloadTime so the animation
    /// finishes right when the reload finishes.
    /// </summary>
    public void PlayReload(float totalDuration)
    {
        // Cancel any in-flight reload animation so we never leave the gun
        // stranded mid-tilt if the player somehow reloads twice.
        if (activeReload != null) StopCoroutine(activeReload);

        activeReload = StartCoroutine(ReloadRoutine(totalDuration));
    }

    private System.Collections.IEnumerator ReloadRoutine(float totalDuration)
    {
        // Calculate phase durations
        float downDuration = totalDuration * tiltDownFraction;
        float upDuration   = totalDuration * tiltUpFraction;
        float holdDuration = Mathf.Max(0f, totalDuration - downDuration - upDuration);

        Quaternion downRotation = originalLocalRotation * Quaternion.Euler(reloadRotationOffset);
        Vector3    downPosition = originalLocalPosition + reloadPositionOffset;

        // PHASE 1 — Tilt down
        float t = 0f;
        while (t < downDuration)
        {
            t += Time.deltaTime;
            float k = tiltDownCurve.Evaluate(Mathf.Clamp01(t / downDuration));
            transform.localRotation = Quaternion.Slerp(originalLocalRotation, downRotation, k);
            transform.localPosition = Vector3.Lerp(originalLocalPosition, downPosition, k);
            yield return null;
        }

        // PHASE 2 — Hold at bottom (player is "loading" the mag)
        if (holdDuration > 0f)
        {
            transform.localRotation = downRotation;
            transform.localPosition = downPosition;
            yield return new WaitForSeconds(holdDuration);
        }

        // PHASE 3 — Return up
        t = 0f;
        while (t < upDuration)
        {
            t += Time.deltaTime;
            float k = tiltUpCurve.Evaluate(Mathf.Clamp01(t / upDuration));
            transform.localRotation = Quaternion.Slerp(downRotation, originalLocalRotation, k);
            transform.localPosition = Vector3.Lerp(downPosition, originalLocalPosition, k);
            yield return null;
        }

        // Snap back exactly to original pose just in case of float drift
        transform.localRotation = originalLocalRotation;
        transform.localPosition = originalLocalPosition;

        activeReload = null;
    }
}