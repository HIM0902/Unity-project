using UnityEngine;

namespace ZombieAI
{
    public class SoundEmitter : MonoBehaviour
    {
        public enum EmitterMode
        {
            PlayerFootsteps,   // Auto-detect movement and emit sounds
            Manual             // Only emit when told (via EmitFromThis or EmitSound)
        }

        [Header("Mode")]
        [SerializeField] private EmitterMode mode = EmitterMode.PlayerFootsteps;

        [Header("Manual Mode")]
        [Tooltip("Loudness for manual EmitFromThis() calls.")]
        [SerializeField] private float manualLoudness = 1f;

        [Header("Footstep Settings")]
        [Tooltip("Seconds between footstep sounds while walking.")]
        [SerializeField] private float walkStepInterval = 0.5f;

        [Tooltip("Loudness of walking footsteps (1.0 = normal hearing range).")]
        [SerializeField] private float walkLoudness = 0.5f;

        [Tooltip("Seconds between footstep sounds while running.")]
        [SerializeField] private float runStepInterval = 0.25f;

        [Tooltip("Loudness of running footsteps.")]
        [SerializeField] private float runLoudness = 1.2f;

        [Tooltip("Loudness while crouching (very quiet).")]
        [SerializeField] private float crouchLoudness = 0.1f;

        [Tooltip("Minimum speed to count as moving (below this = silent).")]
        [SerializeField] private float movementThreshold = 0.2f;

        [Tooltip("Speed above which the player is considered running.")]
        [SerializeField] private float runSpeedThreshold = 5f;

        // Runtime
        private Vector3 lastPosition;
        private float stepTimer;

        // This is set by Movement.cs when crouching
        private bool isCrouching;

        /// Set this from your player controller when crouching.
        /// Crouching makes footsteps nearly silent.
        public bool IsCrouching
        {
            get => isCrouching;
            set => isCrouching = value;
        }

        private void Start()
        {
            lastPosition = transform.position;
        }

        private void Update()
        {
            if (mode != EmitterMode.PlayerFootsteps) return;

            // Calculate movement speed using position delta
            Vector3 currentPos = transform.position;
            float speed = (currentPos - lastPosition).magnitude / Time.deltaTime;
            lastPosition = currentPos;

            // Standing still → no sound
            if (speed < movementThreshold)
            {
                stepTimer = 0f;
                return;
            }

            // Choose loudness and interval based on movement type
            float loudness;
            float interval;

            if (isCrouching)
            {
                loudness = crouchLoudness;
                interval = walkStepInterval * 1.5f; // slower steps when crouching
            }
            else if (speed >= runSpeedThreshold)
            {
                loudness = runLoudness;
                interval = runStepInterval;
            }
            else
            {
                loudness = walkLoudness;
                interval = walkStepInterval;
            }

            // Emit footstep at regular intervals
            stepTimer += Time.deltaTime;
            if (stepTimer >= interval)
            {
                stepTimer = 0f;
                EmitSound(transform.position, loudness);
            }
        }

        /// Emit a sound from this object with the default manual loudness.
        /// Hook this to UnityEvents (doors, traps, radios, etc.).
        public void EmitFromThis()
        {
            EmitSound(transform.position, manualLoudness);
        }

        /// Emit a sound from this object with custom loudness.
        public void EmitFromThis(float loudness)
        {
            EmitSound(transform.position, loudness);
        }

        /// STATIC — Broadcast a sound at a world position to ALL zombies.
        /// Call from anywhere: SoundEmitter.EmitSound(pos, loudness);
        public static void EmitSound(Vector3 position, float loudness = 1f)
        {
            ZombieAIController[] zombies = Object.FindObjectsByType<ZombieAIController>(
                FindObjectsSortMode.None
            );

            foreach (var zombie in zombies)
            {
                zombie.HearSound(position, loudness);
            }

#if UNITY_EDITOR
            // Debug ray at sound position
            Debug.DrawRay(position, Vector3.up * 2f, Color.yellow, 0.5f);
#endif
        }
    }
}