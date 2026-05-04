using UnityEngine;
using UnityEngine.AI;

namespace ZombieAI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class ZombieAIController : MonoBehaviour
    {
        // ───────────────────────── Inspector Fields ─────────────────────────
        [Header("Rubber Band System")]
        public float maxLeashDistance = 45f;

        [Header("References")]
        [Tooltip("The player Transform. Auto-detected via 'Player' tag if left empty.")]
        public Transform player;

        [Header("Hearing (Only Sense)")]
        [Tooltip("Max distance the zombie can hear sounds.")]
        [SerializeField] private float hearingRange = 30f;

        [Header("Proximity (Bumping Into Player)")]
        [Tooltip("Distance at which an ALERTED zombie (Chase/Search/Investigate) attacks on contact.")]
        [SerializeField] private float bumpDetectRange = 2f;

        [Tooltip("Distance at which an IDLE zombie attacks on PHYSICAL contact only.")]
        [SerializeField] private float idleBumpDetectRange = 0.8f;

        [Header("Combat")]
        [Tooltip("Distance at which the zombie can melee the player.")]
        [SerializeField] private float attackRange = 2f;

        [Tooltip("Damage dealt per attack.")]
        [SerializeField] private float attackDamage = 10f;

        [Tooltip("Seconds between attacks (full swing-to-swing cycle).")]
        [SerializeField] private float attackCooldown = 1.5f;

        [Tooltip("Seconds between starting the kick animation and the damage actually landing.")]
        [SerializeField] private float attackWindupTime = 0.4f;

        [Tooltip("If TRUE, damage is applied via Animation Event ONLY. If FALSE, uses Attack Windup Time delay.")]
        [SerializeField] private bool useAnimationEventForDamage = false;

        [Header("Knockback (When Kick Lands)")]
        [SerializeField] private float knockbackForce = 8f;
        [SerializeField] private float knockbackUpwardForce = 2f;

        [Header("Audio — Attack")]
        [Tooltip("Sound played when the zombie BEGINS the kick (whoosh / swing). Optional.")]
        [SerializeField] private AudioClip attackSwingSound;

        [Tooltip("Sound played when the kick LANDS on the player (thump / punch). Optional.")]
        [SerializeField] private AudioClip attackImpactSound;

        [Tooltip("Volume of attack sounds.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float attackSoundVolume = 0.3f;

        [Tooltip("Random pitch range for variety. 0 = none, 0.1 = subtle.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float attackPitchVariation = 0.1f;

        [Header("Audio — Voice")]
        [Tooltip("Snarl/scream played when the zombie spots the player and goes to attack. One picked randomly. Optional.")]
        [SerializeField] private AudioClip[] alertSounds;

        [Tooltip("Random idle moans/groans played while wandering. One picked randomly each time.")]
        [SerializeField] private AudioClip[] idleMoans;

        [Tooltip("Average seconds between idle moans. Set to 0 to disable idle moans.")]
        [SerializeField] private float idleMoanInterval = 8f;

        [Tooltip("Random variance added/subtracted from idle moan interval so it doesn't feel robotic.")]
        [SerializeField] private float idleMoanIntervalVariance = 4f;

        [Tooltip("Volume for voice sounds (alert + idle).")]
        [Range(0f, 1f)]
        [SerializeField] private float voiceVolume = 0.7f;

        [Header("Patrol")]
        [SerializeField] private float patrolRadius = 10f;
        [SerializeField] private float idleWaitTime = 3f;

        [Header("Investigation")]
        [SerializeField] private float investigateTimeout = 6f;
        [SerializeField] private float investigateSpeedMultiplier = 1.2f;

        [Header("Search")]
        [SerializeField] private float searchDuration = 8f;
        [SerializeField] private float searchRadius = 6f;

        [Header("Chase (Heard Repeated Sounds)")]
        [SerializeField] private float chaseSpeedMultiplier = 1.6f;
        [SerializeField] private float chaseTimeout = 6f;

        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;

        // ───────────────────────── Runtime State ────────────────────────────

        public ZombieState CurrentState { get; private set; } = ZombieState.Idle;

        private NavMeshAgent agent;
        private AudioSource audioSource;
        private AudioSource voiceAudioSource;
        private float baseSpeed;

        private float stateTimer;
        private float attackTimer;
        private float idleTimer;
        private bool isIdleStanding;
        private float pendingDamageTimer = -1f;

        private float nextIdleMoanTime;

        private Vector3 spawnPosition;

        private Vector3 lastHeardPosition;
        private bool hasSoundTarget;
        private int soundsHeardRecently;
        private float soundMemoryTimer;
        private const float SoundMemoryDuration = 4f;

        private int searchPointsVisited;
        private const int MaxSearchPoints = 4;

        public System.Action<ZombieState, ZombieState> OnStateChanged;
        public System.Action OnAttackPerformed;

        // ───────────────────────── Unity Lifecycle ──────────────────────────

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            baseSpeed = agent.speed;
            spawnPosition = transform.position;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                ConfigureAudioSource(audioSource);
            }

            voiceAudioSource = gameObject.AddComponent<AudioSource>();
            ConfigureAudioSource(voiceAudioSource);
        }

        private void ConfigureAudioSource(AudioSource src)
        {
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.minDistance = 2f;
            src.maxDistance = 25f;
            src.rolloffMode = AudioRolloffMode.Linear;
        }

        private void Start()
        {
            if (player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj == null)
                    playerObj = GameObject.Find("Player");

                if (playerObj != null)
                {
                    player = playerObj.transform;
                    Debug.Log($"[ZombieAI] Auto-found player: {playerObj.name}");
                }
                else
                {
                    Debug.LogError("[ZombieAI] No player found! Tag or name your player 'Player'.", this);
                }
            }

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                Debug.LogError($"[ZombieAI] {gameObject.name} is NOT on a NavMesh!", this);
            }
            else if (Vector3.Distance(transform.position, hit.position) > 1f)
            {
                transform.position = hit.position;
            }

            ScheduleNextIdleMoan();
            TransitionTo(ZombieState.Idle);
        }

        private void Update()
        {
            if (player != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, player.position);

                if (distanceToPlayer > maxLeashDistance)
                {
                    Vector3 newCatchUpSpot = WaveSpawner.Instance.GetDynamicSpawnPosition();
                    if (agent != null)
                    {
                        agent.Warp(newCatchUpSpot);
                        Debug.LogWarning("RUBBER BAND TRIGGERED: Zombie teleported because it was " + distanceToPlayer + "m away!");
                    }
                }
            }

            if (agent == null || !agent.isOnNavMesh) return;

            attackTimer -= Time.deltaTime;

            if (pendingDamageTimer > 0f)
            {
                pendingDamageTimer -= Time.deltaTime;
                if (pendingDamageTimer <= 0f)
                {
                    pendingDamageTimer = -1f;
                    if (CurrentState == ZombieState.Attack)
                    {
                        ApplyDamageToPlayer();
                    }
                }
            }

            if (soundsHeardRecently > 0)
            {
                soundMemoryTimer -= Time.deltaTime;
                if (soundMemoryTimer <= 0f)
                    soundsHeardRecently = 0;
            }

            UpdateIdleMoans();

            if (player != null && CurrentState != ZombieState.Attack)
            {
                float distToPlayer = Vector3.Distance(transform.position, player.position);

                float effectiveBumpRange = (CurrentState == ZombieState.Idle)
                    ? idleBumpDetectRange
                    : bumpDetectRange;

                if (distToPlayer <= effectiveBumpRange)
                {
                    TransitionTo(ZombieState.Attack);
                    return;
                }
            }

            switch (CurrentState)
            {
                case ZombieState.Idle:              UpdateIdle(); break;
                case ZombieState.InvestigateSound:  UpdateInvestigate(); break;
                case ZombieState.Chase:             UpdateChase(); break;
                case ZombieState.SearchArea:        UpdateSearch(); break;
                case ZombieState.Attack:            UpdateAttack(); break;
            }
        }

        // ───────────────────────── State Transitions ────────────────────────

        private void TransitionTo(ZombieState newState)
        {
            if (newState == CurrentState) return;

            ZombieState prev = CurrentState;
            OnExitState(CurrentState);
            CurrentState = newState;
            OnEnterState(newState);

            HandleVoiceForStateChange(prev, newState);

            OnStateChanged?.Invoke(prev, newState);

            #if UNITY_EDITOR
            Debug.Log($"[ZombieAI] {gameObject.name}: {prev} → {newState}");
            #endif
        }

        // Plays alert snarl when zombie escalates from a calm state to combat.
        // Investigate sound was removed — use death sounds in Health.cs instead.
        private void HandleVoiceForStateChange(ZombieState prev, ZombieState next)
        {
            bool wasNonCombat = prev == ZombieState.Idle ||
                                prev == ZombieState.InvestigateSound ||
                                prev == ZombieState.SearchArea;

            bool nowCombat = next == ZombieState.Attack || next == ZombieState.Chase;

            if (wasNonCombat && nowCombat)
            {
                PlayRandomVoice(alertSounds);
            }
        }

        private void OnEnterState(ZombieState state)
        {
            stateTimer = 0f;

            switch (state)
            {
                case ZombieState.Idle:
                    agent.speed = baseSpeed;
                    isIdleStanding = false;
                    SetRandomPatrolPoint();
                    break;

                case ZombieState.InvestigateSound:
                    agent.speed = baseSpeed * investigateSpeedMultiplier;
                    if (hasSoundTarget) agent.SetDestination(lastHeardPosition);
                    break;

                case ZombieState.Chase:
                    agent.speed = baseSpeed * chaseSpeedMultiplier;
                    if (hasSoundTarget) agent.SetDestination(lastHeardPosition);
                    break;

                case ZombieState.SearchArea:
                    agent.speed = baseSpeed * 0.6f;
                    searchPointsVisited = 0;
                    SetRandomSearchPoint();
                    break;

                case ZombieState.Attack:
                    attackTimer = 0f;
                    agent.speed = 0f;
                    agent.ResetPath();
                    break;
            }
        }

        private void OnExitState(ZombieState state)
        {
            if (state == ZombieState.InvestigateSound || state == ZombieState.Chase)
                hasSoundTarget = false;

            if (state == ZombieState.Attack)
                pendingDamageTimer = -1f;
        }

        // ───────────────────────── State Updates ────────────────────────────

        private void UpdateIdle()
        {
            if (hasSoundTarget) { TransitionTo(ZombieState.InvestigateSound); return; }

            if (isIdleStanding)
            {
                idleTimer -= Time.deltaTime;
                if (idleTimer <= 0f)
                {
                    isIdleStanding = false;
                    SetRandomPatrolPoint();
                }
                return;
            }

            if (!agent.hasPath && !agent.pathPending)
            {
                SetRandomPatrolPoint();
                return;
            }

            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                isIdleStanding = true;
                idleTimer = idleWaitTime + Random.Range(0f, 3f);
                agent.ResetPath();
            }
        }

        private void UpdateInvestigate()
        {
            stateTimer += Time.deltaTime;

            bool arrived = !agent.pathPending && agent.remainingDistance < 1.5f;
            if (arrived || stateTimer >= investigateTimeout)
            {
                if (soundsHeardRecently >= 2) TransitionTo(ZombieState.Chase);
                else                          TransitionTo(ZombieState.SearchArea);
            }
        }

        private void UpdateChase()
        {
            stateTimer += Time.deltaTime;

            if (hasSoundTarget)
            {
                agent.SetDestination(lastHeardPosition);
                stateTimer = 0f;
                hasSoundTarget = false;
            }

            bool arrived = !agent.pathPending && agent.remainingDistance < 1.5f;
            if (arrived || stateTimer >= chaseTimeout)
            {
                TransitionTo(ZombieState.SearchArea);
            }
        }

        private void UpdateSearch()
        {
            stateTimer += Time.deltaTime;

            if (hasSoundTarget) { TransitionTo(ZombieState.InvestigateSound); return; }

            if (!agent.pathPending && agent.remainingDistance < 1f)
            {
                searchPointsVisited++;
                if (searchPointsVisited >= MaxSearchPoints || stateTimer >= searchDuration)
                {
                    TransitionTo(ZombieState.Idle);
                    return;
                }
                SetRandomSearchPoint();
            }
        }

        private void UpdateAttack()
        {
            if (player == null) return;

            float distToPlayer = Vector3.Distance(transform.position, player.position);

            Vector3 lookDir = (player.position - transform.position).normalized;
            lookDir.y = 0f;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(lookDir),
                    Time.deltaTime * 8f
                );
            }

            if (distToPlayer > attackRange * 1.5f)
            {
                lastHeardPosition   = player.position;
                hasSoundTarget      = true;
                soundsHeardRecently = 3;
                soundMemoryTimer    = SoundMemoryDuration;
                TransitionTo(ZombieState.Chase);
                return;
            }

            if (distToPlayer > attackRange)
            {
                agent.speed = baseSpeed * 0.5f;
                agent.SetDestination(player.position);
            }
            else
            {
                agent.speed = 0f;
                if (agent.hasPath) agent.ResetPath();

                if (attackTimer <= 0f)
                {
                    PerformAttack();
                    attackTimer = attackCooldown;
                }
            }
        }

        // ───────────────────────── Hearing ──────────────────────────────────

        public void HearSound(Vector3 position, float loudness = 1f)
        {
            float dist = Vector3.Distance(transform.position, position);
            if (dist > hearingRange * loudness) return;

            lastHeardPosition = position;
            hasSoundTarget = true;
            soundsHeardRecently++;
            soundMemoryTimer = SoundMemoryDuration;

            switch (CurrentState)
            {
                case ZombieState.Idle:              TransitionTo(ZombieState.InvestigateSound); break;
                case ZombieState.InvestigateSound:  agent.SetDestination(lastHeardPosition); stateTimer = 0f; break;
                case ZombieState.SearchArea:        TransitionTo(ZombieState.InvestigateSound); break;
            }
        }

        // ───────────────────────── Idle Moans ────────────────────────────────

        private void UpdateIdleMoans()
        {
            if (idleMoanInterval <= 0f || idleMoans == null || idleMoans.Length == 0)
                return;

            if (CurrentState != ZombieState.Idle && CurrentState != ZombieState.SearchArea)
                return;

            if (Time.time >= nextIdleMoanTime)
            {
                PlayRandomVoice(idleMoans);
                ScheduleNextIdleMoan();
            }
        }

        private void ScheduleNextIdleMoan()
        {
            nextIdleMoanTime = Time.time + idleMoanInterval +
                               Random.Range(-idleMoanIntervalVariance, idleMoanIntervalVariance);
        }

        // ───────────────────────── Actions ──────────────────────────────────

        private void PerformAttack()
        {
            OnAttackPerformed?.Invoke();

            PlayAttackSound(attackSwingSound);

            if (useAnimationEventForDamage)
            {
                // Wait for AnimationEvent_DealDamage to fire
            }
            else
            {
                pendingDamageTimer = attackWindupTime;
            }

            #if UNITY_EDITOR
            Debug.Log($"[ZombieAI] {gameObject.name} starts kick — damage in {attackWindupTime}s");
            #endif
        }

        public void AnimationEvent_DealDamage()
        {
            if (CurrentState != ZombieState.Attack) return;
            ApplyDamageToPlayer();
        }

        private void ApplyDamageToPlayer()
        {
            if (player == null) return;

            if (Vector3.Distance(transform.position, player.position) > attackRange * 1.2f)
                return;

            if (player.TryGetComponent(out PlayerHealth health))
            {
                health.TakeDamage(attackDamage);
            }

            PlayAttackSound(attackImpactSound);
            ApplyKnockbackToPlayer();
        }

        private void PlayAttackSound(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;

            audioSource.pitch = 1f + Random.Range(-attackPitchVariation, attackPitchVariation);
            audioSource.PlayOneShot(clip, attackSoundVolume);
        }

        private void PlayRandomVoice(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0 || voiceAudioSource == null) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            voiceAudioSource.pitch = 1f + Random.Range(-attackPitchVariation, attackPitchVariation);
            voiceAudioSource.PlayOneShot(clip, voiceVolume);
        }

        private void ApplyKnockbackToPlayer()
        {
            if (player == null) return;

            Vector3 horizontalDir = player.position - transform.position;
            horizontalDir.y = 0f;
            horizontalDir.Normalize();

            Vector3 force = horizontalDir * knockbackForce + Vector3.up * knockbackUpwardForce;

            if (player.TryGetComponent(out Rigidbody rb) && !rb.isKinematic)
            {
                rb.AddForce(force, ForceMode.Impulse);
                return;
            }

            player.SendMessage("ApplyKnockback", force, SendMessageOptions.DontRequireReceiver);
        }

        // ───────────────────────── Wave Scaling ─────────────────────────────

        public void ApplySpeedMultiplier(float multiplier)
        {
            baseSpeed = baseSpeed * multiplier;

            if (agent != null)
            {
                agent.speed = baseSpeed;
                Debug.Log("zombie spawned! base speed is now: " + agent.speed + " with a multiplier of: " + multiplier);
            }
        }

        // ───────────────────────── Navigation Helpers ───────────────────────

        private void SetRandomPatrolPoint()
        {
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
            Vector3 randomPoint = spawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        private void SetRandomSearchPoint()
        {
            Vector3 searchCenter = lastHeardPosition != Vector3.zero ? lastHeardPosition : transform.position;

            Vector2 randomCircle = Random.insideUnitCircle * searchRadius;
            Vector3 randomPoint = searchCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        // ───────────────────────── Gizmos ───────────────────────────────────

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            Gizmos.color = new Color(1f, 0.9f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, hearingRange);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, bumpDetectRange);

            Gizmos.color = new Color(0.6f, 0.3f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, idleBumpDetectRange);

            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Vector3 origin = Application.isPlaying ? spawnPosition : transform.position;
            Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.1f);
            Gizmos.DrawWireSphere(origin, patrolRadius);

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.5f,
                $"State: {CurrentState}" +
                (soundsHeardRecently > 0 ? $"  Sounds: {soundsHeardRecently}" : "")
            );
        }
        #endif
    }
}