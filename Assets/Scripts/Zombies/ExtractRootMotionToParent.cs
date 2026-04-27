using UnityEngine;

public class ExtractRootMotionToParent : MonoBehaviour
{
    [Tooltip("The animated child transform that is drifting/moving (Armature/root bone object).")]
    public Transform animatedRoot;

    [Tooltip("If true, do not apply vertical (Y) motion to the parent.")]
    public bool lockY = true;

    [Tooltip("If true, apply rotation from the animated root to the parent.")]
    public bool applyRotation = true;

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

    void LateUpdate()
    {
        // 1) Apply horizontal delta from animated root
        Vector3 localDeltaPos = animatedRoot.localPosition - startLocalPos;
        Vector3 worldDelta = transform.TransformVector(localDeltaPos);
        worldDelta.y = 0f; // keep horizontal only
        transform.position += worldDelta;

        if (applyRotation)
        {
            Quaternion localDeltaRot = animatedRoot.localRotation * Quaternion.Inverse(startLocalRot);
            transform.rotation = localDeltaRot * transform.rotation;
        }

        // Reset animated root so it doesn't drift away
        animatedRoot.localPosition = startLocalPos;
        animatedRoot.localRotation = startLocalRot;

        // 2) Snap to ground / terrain so we follow vertical changes
        Ray ray = new Ray(transform.position + Vector3.up * 2f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            // Put the parent at the ground hit point (keep any offset you need)
            transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
        }
    }
}