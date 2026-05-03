using UnityEngine;

public class ExtractRootMotionToParent : MonoBehaviour
{
    [Tooltip("The animated child transform that is drifting/moving (Armature/root bone object).")]
    public Transform animatedRoot;

    [Tooltip("If true, do not apply vertical (Y) motion to the parent.")]
    public bool lockY = true;

    [Tooltip("If true, apply rotation from the animated root to the parent.")]
    public bool applyRotation = true;

    [Header("Ground Snap")]
    [Tooltip("If true, raycast down to keep the parent on the ground.")]
    public bool snapToGround = true;

    [Tooltip("Raycast start height above the parent.")]
    public float groundRayStart = 2f;

    [Tooltip("Raycast distance downward.")]
    public float groundRayDistance = 10f;

    [Header("Runtime")]
    [Tooltip("When false, this script will NOT move the parent (prevents nudging).")]
    [SerializeField] private bool allowMotion = true;

    private Vector3 startLocalPos;
    private Quaternion startLocalRot;

    void Start()
    {
        if (animatedRoot == null)
        {
            Debug.LogError("ExtractRootMotionToParent: animatedRoot is not assigned!", this);
            enabled = false;
            return;
        }

        startLocalPos = animatedRoot.localPosition;
        startLocalRot = animatedRoot.localRotation;
    }

    /// <summary>
    /// Call this when the zombie dies to stop all root-motion extraction nudging.
    /// </summary>
    public void StopMotion()
    {
        allowMotion = false;

        // Reset animated root so it doesn't keep drifting visually
        if (animatedRoot != null)
        {
            animatedRoot.localPosition = startLocalPos;
            animatedRoot.localRotation = startLocalRot;
        }
    }

    void LateUpdate()
    {
        if (animatedRoot == null) return;

        // Always reset the animated root so the skeleton doesn't drift away from the parent
        // (even if we're not allowing motion).
        if (!allowMotion)
        {
            animatedRoot.localPosition = startLocalPos;
            animatedRoot.localRotation = startLocalRot;
            return;
        }

        // 1) Apply delta from animated root to parent
        Vector3 localDeltaPos = animatedRoot.localPosition - startLocalPos;
        Vector3 worldDelta = transform.TransformVector(localDeltaPos);

        if (lockY) worldDelta.y = 0f;

        transform.position += worldDelta;

        if (applyRotation)
        {
            Quaternion localDeltaRot = animatedRoot.localRotation * Quaternion.Inverse(startLocalRot);
            transform.rotation = localDeltaRot * transform.rotation;
        }

        // Reset animated root so it doesn't drift away
        animatedRoot.localPosition = startLocalPos;
        animatedRoot.localRotation = startLocalRot;

        // 2) Snap to ground / terrain so we follow vertical changes (optional)
        if (snapToGround)
        {
            Ray ray = new Ray(transform.position + Vector3.up * groundRayStart, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, groundRayDistance))
            {
                transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
            }
        }
    }
}