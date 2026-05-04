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
        [SerializeField] private float hearingRange = 30f;

        [Header("Proximity (Bumping Into Player)")]
        [SerializeField] private float bumpDetectRange = 2f;
        [SerializeField] private float idleBumpDetectRange = 0.8f;

        [Header("Combat")]
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackDamage = 10f;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private float attackWindupTime = 0.4f;
        [SerializeField] private bool useAnimationEventForDamage = false;

        [Header("Knockback (When Kick Lands)")]
        [SerializeField] private float knockbackForce = 8f;
        [SerializeField] private float knockbackUpwardForce = 2f;

        [Header("Audio — Attack")]
        [SerializeField] private AudioClip attackSwingSound;
        [SerializeField] private AudioClip attackImpactSound;

        [Header("Audio — Voice")]
        [SerializeField] private AudioClip[] alertSounds;
        [SerializeField] private AudioClip[] idleMoans;
        [SerializeField] private float idleMoanInterval = 8f;
        [SerializeField] private float idleMoanIntervalVariance = 4f;

        // ─── Volume CONSTANTS (hardcoded so multiple zombies don't blow your ears) ───
        // Quiet by design. Each zombie individually is barely audible — 5+ zombies
        // start to feel atmospheric without overwhelming the player.
        private const float ATTACK_VOLUME       = 0.05f;
        private const float VOICE_VOLUME        = 0.05f;
        private const float ATTACK_PITCH_RANGE  = 0.1f;
        // ─────────────────────────────────────────────────────────────────────

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
            else
            {
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
            src.volume = 1f; // master volume on the source — actual loudness is set in PlayOneShot calls
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
        }

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

        // FIXED: volume is now hardcoded to ATTACK_VOLUME (0.05f).
        // No inspector slider to fiddle with — set it once here, every zombie inherits.
        private void PlayAttackSound(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;

            audioSource.pitch = 1f + Random.Range(-ATTACK_PITCH_RANGE, ATTACK_PITCH_RANGE);
            audioSource.PlayOneShot(clip, ATTACK_VOLUME);
        }

        // FIXED: volume is now hardcoded to VOICE_VOLUME (0.05f).
        private void PlayRandomVoice(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0 || voiceAudioSource == null) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            voiceAudioSource.pitch = 1f + Random.Range(-ATTACK_PITCH_RANGE, ATTACK_PITCH_RANGE);
            voiceAudioSource.PlayOneShot(clip, VOICE_VOLUME);
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