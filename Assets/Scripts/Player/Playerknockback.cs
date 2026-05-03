using UnityEngine;

/// <summary>
/// Receives knockback from ZombieAIController.
/// Uses LateUpdate so it runs AFTER your movement script — this prevents your
/// own movement code from overwriting the push every frame.
/// </summary>
public class PlayerKnockback : MonoBehaviour
{
    [Tooltip("How fast the knockback fades out. Higher = snappier recovery.")]
    [SerializeField] private float decayRate = 5f;

    [Tooltip("Multiplier on the incoming force.")]
    [SerializeField] private float forceScale = 1f;

    [Tooltip("Strip vertical force so the kick doesn't launch you up. Recommended ON for transform-based players without gravity.")]
    [SerializeField] private bool horizontalOnly = true;

    [Tooltip("Print debug logs to verify the script is receiving messages.")]
    [SerializeField] private bool debugLogs = true;

    private Vector3 currentVelocity;

    private void Awake()
    {
        if (debugLogs)
        {
            Debug.Log($"[PlayerKnockback] Initialized on '{gameObject.name}'. Tag: '{gameObject.tag}'.", this);
            if (gameObject.tag != "Player" && gameObject.name != "Player")
            {
                Debug.LogWarning($"[PlayerKnockback] This GameObject is NOT tagged or named 'Player' — the zombie won't find it! Either tag this object as Player or rename it.", this);
            }
        }
    }

    /// <summary>
    /// Called by ZombieAIController via SendMessage when the kick lands.
    /// The method name MUST be exactly "ApplyKnockback".
    /// </summary>
    public void ApplyKnockback(Vector3 force)
    {
        if (horizontalOnly) force.y = 0f;
        currentVelocity += force * forceScale;

        if (debugLogs)
            Debug.Log($"[PlayerKnockback] RECEIVED kick! Force: {force}. New velocity: {currentVelocity}", this);
    }

    // LateUpdate runs AFTER all Update() calls — including your movement script.
    // So the knockback is added on top instead of being overwritten.
    private void LateUpdate()
    {
        if (currentVelocity.sqrMagnitude < 0.0001f)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        transform.position += currentVelocity * Time.deltaTime;
        currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, Time.deltaTime * decayRate);
    }
}