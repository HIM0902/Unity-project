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

        [Tooltip("Distance at which an IDLE zombie attacks on PHYSICAL contact only. Keep small — zombie is unaware so player should be able to sneak past.")]
        [SerializeField] private float idleBumpDetectRange = 0.8f;

        [Header("Combat")]
        [Tooltip("Distance at which the zombie can melee the player.")]
        [SerializeField] private float attackRange = 2f;

        [Tooltip("Damage dealt per attack.")]
        [SerializeField] private float attackDamage = 10f;

        [Tooltip("Seconds between attacks (full swing-to-swing cycle). Should be LARGER than Attack Windup Time.")]
        [SerializeField] private float attackCooldown = 1.5f;

        [Tooltip("Seconds between starting the kick animation and the damage actually landing. Tune this to match the foot-impact frame of your kick animation. Typical kicks: 0.3 - 0.5s.")]
        [SerializeField] private float attackWindupTime = 0.4f;

        [Tooltip("If TRUE, damage is applied via Animation Event ONLY. If FALSE, uses Attack Windup Time delay.")]
        [SerializeField] private bool useAnimationEventForDamage = false;

        [Header("Knockback (When Kick Lands)")]
        [Tooltip("How hard the player gets pushed away horizontally when the kick connects.")]
        [SerializeField] private float knockbackForce = 8f;

        [Tooltip("Small upward kick to make the player stagger / lift slightly. Set to 0 for a flat shove.")]
        [SerializeField] private float knockbackUpwardForce = 2f;

        [Header("Patrol")]
        [Tooltip("Radius for random wander destinations around spawn point.")]
        [SerializeField] private float patrolRadius = 10f;

        [Tooltip("Seconds the zombie stands still BETWEEN patrol points (after reaching one).")]
        [SerializeField] private float idleWaitTime = 3f;

        [Header("Investigation")]
        [Tooltip("Seconds before the zombie loses interest at a sound location.")]
        [SerializeField] private float investigateTimeout = 6f;

        [Tooltip("Speed multiplier when investigating a sound.")]
        [SerializeField] private float investigateSpeedMultiplier = 1.2f;

        [Header("Search")]
        [Tooltip("Seconds spent searching an area before giving up.")]
        [SerializeField] private float searchDuration = 8f;

        [Tooltip("Radius the zombie wanders while searching.")]
        [SerializeField] private float searchRadius = 6f;

        [Header("Chase (Heard Repeated Sounds)")]
        [Tooltip("Speed multiplier when chasing toward a sound source.")]
        [SerializeField] private float chaseSpeedMultiplier = 1.6f;

        [Tooltip("Seconds moving toward last sound before giving up.")]
        [SerializeField] private float chaseTimeout = 6f;

        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;

        // ───────────────────────── Runtime State ────────────────────────────

        public ZombieState CurrentState { get; private set; } = ZombieState.Idle;

        private NavMeshAgent agent;
        private float baseSpeed;

        // Timers
        private float stateTimer;
        private float attackTimer;
        private float idleTimer;
        private bool isIdleStanding;
        private float pendingDamageTimer = -1f;

        // Origins
        private Vector3 spawnPosition;

        // Sound tracking
        private Vector3 lastHeardPosition;
        private bool hasSoundTarget;
        private int soundsHeardRecently;
        private float soundMemoryTimer;
        private const float SoundMemoryDuration = 4f;

        // Search
        private int searchPointsVisited;
        private const int MaxSearchPoints = 4;

        // Events
        public System.Action<ZombieState, ZombieState> OnStateChanged;
        public System.Action OnAttackPerformed;

        // ───────────────────────── Unity Lifecycle ──────────────────────────

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            baseSpeed = agent.speed;
            spawnPosition = transform.position;
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
                Debug.LogError($"[ZombieAI] {gameObject.name} is NOT on a NavMesh! Add NavMeshSurface and Bake.", this);
            }
            else if (Vector3.Distance(transform.position, hit.position) > 1f)
            {
                transform.position = hit.position;
            }

            TransitionTo(ZombieState.Idle);
        }

        private void Update()
        {
            // 1. RUBBER BAND CHECK
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

            // 2. SAFETY CHECK
            if (agent == null || !agent.isOnNavMesh) return;

            // 3. TIMERS
            attackTimer -= Time.deltaTime;

            // Pending damage countdown — when it hits 0, the kick "lands"
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

            // 4. BUMP DETECTION
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

            // 5. STATE MACHINE
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

            OnStateChanged?.Invoke(prev, newState);

            #if UNITY_EDITOR
            Debug.Log($"[ZombieAI] {gameObject.name}: {prev} → {newState}");
            #endif
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
                    if (hasSoundTarget)
                        agent.SetDestination(lastHeardPosition);
                    break;

                case ZombieState.Chase:
                    agent.speed = baseSpeed * chaseSpeedMultiplier;
                    if (hasSoundTarget)
                        agent.SetDestination(lastHeardPosition);
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
            {
                hasSoundTarget = false;
            }

            if (state == ZombieState.Attack)
            {
                pendingDamageTimer = -1f;
            }
        }

        // ───────────────────────── State Updates ────────────────────────────

        private void UpdateIdle()
        {
            if (hasSoundTarget)
            {
                TransitionTo(ZombieState.InvestigateSound);
                return;
            }

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
                if (soundsHeardRecently >= 2)
                    TransitionTo(ZombieState.Chase);
                else
                    TransitionTo(ZombieState.SearchArea);
                return;
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
            if (arrived)
            {
                TransitionTo(ZombieState.SearchArea);
                return;
            }

            if (stateTimer >= chaseTimeout)
            {
                TransitionTo(ZombieState.SearchArea);
                return;
            }
        }

        private void UpdateSearch()
        {
            stateTimer += Time.deltaTime;

            if (hasSoundTarget)
            {
                TransitionTo(ZombieState.InvestigateSound);
                return;
            }

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
                case ZombieState.Idle:
                    TransitionTo(ZombieState.InvestigateSound);
                    break;

                case ZombieState.InvestigateSound:
                    agent.SetDestination(lastHeardPosition);
                    stateTimer = 0f;
                    break;

                case ZombieState.SearchArea:
                    TransitionTo(ZombieState.InvestigateSound);
                    break;

                case ZombieState.Chase:
                    break;

                case ZombieState.Attack:
                    break;
            }
        }

        // ───────────────────────── Actions ──────────────────────────────────

        private void PerformAttack()
        {
            OnAttackPerformed?.Invoke();

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

        /// <summary>
        /// Called by an Animation Event on the kick-impact frame.
        /// </summary>
        public void AnimationEvent_DealDamage()
        {
            if (CurrentState != ZombieState.Attack) return;
            ApplyDamageToPlayer();
        }

        // FIXED: now applies BOTH damage AND knockback at the impact moment.
        private void ApplyDamageToPlayer()
        {
            if (player == null) return;

            // Final range check — player may have escaped during the windup.
            if (Vector3.Distance(transform.position, player.position) > attackRange * 1.2f)
                return;

            // Damage
            if (player.TryGetComponent(out PlayerHealth health))
            {
                health.TakeDamage(attackDamage);
            }

            // Knockback — push the player backwards from the zombie
            ApplyKnockbackToPlayer();
        }

        // Pushes the player away from the zombie when the kick connects.
        // Works with Rigidbody players OR custom players via the PlayerKnockback script.
        private void ApplyKnockbackToPlayer()
        {
            if (player == null)
            {
                Debug.LogWarning("[ZombieAI] Knockback skipped — player reference is null!", this);
                return;
            }

            // Direction from zombie to player, flat (no vertical bias from height diff)
            Vector3 horizontalDir = player.position - transform.position;
            horizontalDir.y = 0f;
            horizontalDir.Normalize();

            // Combined force vector: horizontal push + small upward stagger
            Vector3 force = horizontalDir * knockbackForce + Vector3.up * knockbackUpwardForce;

            Debug.Log($"[ZombieAI] Firing knockback at '{player.name}'. Force: {force}");

            // CASE 1: Rigidbody player → use physics impulse
            if (player.TryGetComponent(out Rigidbody rb) && !rb.isKinematic)
            {
                rb.AddForce(force, ForceMode.Impulse);
                Debug.Log("[ZombieAI] Used Rigidbody.AddForce path.");
                return;
            }

            // CASE 2: Custom player → message any script with ApplyKnockback(Vector3).
            // The PlayerKnockback script catches this.
            player.SendMessage("ApplyKnockback", force, SendMessageOptions.DontRequireReceiver);
            Debug.Log("[ZombieAI] Sent SendMessage path. If you don't see [PlayerKnockback] RECEIVED, the script isn't on the right GameObject.");
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
            {
                agent.SetDestination(hit.position);
            }
        }

        private void SetRandomSearchPoint()
        {
            Vector3 searchCenter = lastHeardPosition != Vector3.zero
                ? lastHeardPosition
                : transform.position;

            Vector2 randomCircle = Random.insideUnitCircle * searchRadius;
            Vector3 randomPoint = searchCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }

        // ───────────────────────── Gizmos ───────────────────────────────────

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            Gizmos.color = new Color(1f, 0.9f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, hearingRange);
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);
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

            if (Application.isPlaying && lastHeardPosition != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(lastHeardPosition, 0.4f);
                Gizmos.DrawLine(transform.position + Vector3.up, lastHeardPosition + Vector3.up * 0.5f);
            }

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.5f,
                $"State: {CurrentState}" +
                (soundsHeardRecently > 0 ? $"  Sounds: {soundsHeardRecently}" : "")
            );
        }
        #endif
    }
}