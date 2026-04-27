using UnityEngine;
using UnityEngine.AI;

namespace ZombieAI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class ZombieAIController : MonoBehaviour
    {
        // ───────────────────────── Inspector Fields ─────────────────────────

        [Header("References")]
        [Tooltip("The player Transform. Auto-detected via 'Player' tag if left empty.")]
        public Transform player;

        [Header("Hearing (Only Sense)")]
        [Tooltip("Max distance the zombie can hear sounds.")]
        [SerializeField] private float hearingRange = 30f;

        [Header("Proximity (Bumping Into Player)")]
        [Tooltip("Distance at which the zombie detects the player by touch/proximity.")]
        [SerializeField] private float bumpDetectRange = 1.5f;

        [Header("Combat")]
        [Tooltip("Distance at which the zombie can melee the player.")]
        [SerializeField] private float attackRange = 2f;

        [Tooltip("Damage dealt per attack.")]
        [SerializeField] private float attackDamage = 10f;

        [Tooltip("Seconds between attacks.")]
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("Patrol")]
        [Tooltip("Radius for random wander destinations around spawn point.")]
        [SerializeField] private float patrolRadius = 10f;

        [Tooltip("Seconds the zombie stands still before wandering.")]
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

        // Origins
        private Vector3 spawnPosition;

        // Sound tracking
        private Vector3 lastHeardPosition;
        private bool hasSoundTarget;
        private int soundsHeardRecently;        // multiple sounds = more aggressive
        private float soundMemoryTimer;          // resets sound count after silence
        private const float SoundMemoryDuration = 4f;

        // Search
        private int searchPointsVisited;
        private const int MaxSearchPoints = 4;

        // Events (hook audio / VFX here)
        public System.Action<ZombieState, ZombieState> OnStateChanged;

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

            // Verify NavMesh
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
            attackTimer -= Time.deltaTime;

            // Decay sound memory over time (zombie forgets if no new sounds)
            if (soundsHeardRecently > 0)
            {
                soundMemoryTimer -= Time.deltaTime;
                if (soundMemoryTimer <= 0f)
                    soundsHeardRecently = 0;
            }

            // Check bump proximity in every state (blind zombie walks into player)
            if (player != null && CurrentState != ZombieState.Attack)
            {
                float distToPlayer = Vector3.Distance(transform.position, player.position);
                if (distToPlayer <= bumpDetectRange)
                {
                    TransitionTo(ZombieState.Attack);
                    return;
                }
            }

            switch (CurrentState)
            {
                case ZombieState.Idle:              UpdateIdle(); break;
                case ZombieState.InvestigateSound:   UpdateInvestigate(); break;
                case ZombieState.Chase:              UpdateChase(); break;
                case ZombieState.SearchArea:         UpdateSearch(); break;
                case ZombieState.Attack:             UpdateAttack(); break;
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
                    idleTimer = idleWaitTime;
                    isIdleStanding = true;
                    agent.ResetPath();
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
        }

        // ───────────────────────── State Updates ────────────────────────────

        // ── IDLE / PATROL ──
        // Zombie wanders aimlessly. Only reacts to sounds.
        private void UpdateIdle()
        {
            // Sound heard → Investigate
            if (hasSoundTarget)
            {
                TransitionTo(ZombieState.InvestigateSound);
                return;
            }

            // Stand still phase
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

            // Walking to patrol point → arrived → stand still again
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                isIdleStanding = true;
                idleTimer = idleWaitTime + Random.Range(0f, 3f);
                agent.ResetPath();
            }
        }

        // ── INVESTIGATE SOUND ──
        // Zombie heard something. Moves to the sound location.
        private void UpdateInvestigate()
        {
            stateTimer += Time.deltaTime;

            // Arrived at sound source or timed out
            bool arrived = !agent.pathPending && agent.remainingDistance < 1.5f;
            if (arrived || stateTimer >= investigateTimeout)
            {
                // Multiple sounds heard recently → escalate to Chase
                if (soundsHeardRecently >= 2)
                {
                    TransitionTo(ZombieState.Chase);
                }
                else
                {
                    // Just one sound → search around the area then give up
                    TransitionTo(ZombieState.SearchArea);
                }
                return;
            }
        }

        // ── CHASE ──
        // Zombie heard repeated sounds — aggressively rushes toward last heard position.
        // Still BLIND — just running to where sounds came from.
        private void UpdateChase()
        {
            stateTimer += Time.deltaTime;

            // New sound heard → update destination and reset timer
            if (hasSoundTarget)
            {
                agent.SetDestination(lastHeardPosition);
                stateTimer = 0f;
                hasSoundTarget = false;
            }

            // Arrived at last sound position → Search the area
            bool arrived = !agent.pathPending && agent.remainingDistance < 1.5f;
            if (arrived)
            {
                TransitionTo(ZombieState.SearchArea);
                return;
            }

            // No new sounds for too long → give up to Search
            if (stateTimer >= chaseTimeout)
            {
                TransitionTo(ZombieState.SearchArea);
                return;
            }
        }

        // ── SEARCH AREA ──
        // Zombie looks around the last heard location randomly.
        private void UpdateSearch()
        {
            stateTimer += Time.deltaTime;

            // New sound → go investigate
            if (hasSoundTarget)
            {
                TransitionTo(ZombieState.InvestigateSound);
                return;
            }

            // Wander between random search points
            if (!agent.pathPending && agent.remainingDistance < 1f)
            {
                searchPointsVisited++;
                if (searchPointsVisited >= MaxSearchPoints || stateTimer >= searchDuration)
                {
                    // Give Up → return to Idle
                    TransitionTo(ZombieState.Idle);
                    return;
                }
                SetRandomSearchPoint();
            }
        }

        // ── ATTACK ──
        // Zombie bumped into the player. Melee attack.
        private void UpdateAttack()
        {
            if (player == null) return;

            float distToPlayer = Vector3.Distance(transform.position, player.position);

            // Face the player
            Vector3 lookDir = (player.position - transform.position).normalized;
            lookDir.y = 0f;
            if (lookDir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(lookDir),
                    Time.deltaTime * 5f
                );

            // Player escaped attack range → escaping makes noise → Chase!
            if (distToPlayer > attackRange * 1.3f)
            {
                lastHeardPosition = player.position;
                hasSoundTarget = true;
                soundsHeardRecently = 3; // very aggressive
                soundMemoryTimer = SoundMemoryDuration;
                TransitionTo(ZombieState.Chase);
                return;
            }

            // Attack on cooldown
            if (attackTimer <= 0f)
            {
                PerformAttack();
                attackTimer = attackCooldown;
            }
        }

        // ───────────────────────── Hearing (Only Sense) ─────────────────────

        /// <summary>
        /// Call from SoundEmitter when the player makes noise.
        /// Loudness multiplies hearing range (1.0 = normal, 0.3 = quiet, 2.0 = loud).
        /// </summary>
        public void HearSound(Vector3 position, float loudness = 1f)
        {
            float dist = Vector3.Distance(transform.position, position);
            if (dist > hearingRange * loudness) return;

            lastHeardPosition = position;
            hasSoundTarget = true;

            // Track recent sounds (more sounds = more aggressive)
            soundsHeardRecently++;
            soundMemoryTimer = SoundMemoryDuration;

            switch (CurrentState)
            {
                case ZombieState.Idle:
                    TransitionTo(ZombieState.InvestigateSound);
                    break;

                case ZombieState.InvestigateSound:
                    // Redirect to new sound
                    agent.SetDestination(lastHeardPosition);
                    stateTimer = 0f;
                    break;

                case ZombieState.SearchArea:
                    // Heard sound while searching → investigate
                    TransitionTo(ZombieState.InvestigateSound);
                    break;

                case ZombieState.Chase:
                    // Update target (UpdateChase reads the flag)
                    break;

                case ZombieState.Attack:
                    // Already attacking, ignore
                    break;
            }
        }

        // ───────────────────────── Actions ──────────────────────────────────

        private void PerformAttack()
        {
            if (player != null && player.TryGetComponent(out PlayerHealth health))
            {
                health.TakeDamage(attackDamage);
            }

            #if UNITY_EDITOR
            Debug.Log($"[ZombieAI] {gameObject.name} attacks for {attackDamage} damage!");
            #endif
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

            // Hearing range (yellow — the zombie's ONLY sense)
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, hearingRange);
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, hearingRange);

            // Bump detection range (orange)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, bumpDetectRange);

            // Attack range (red)
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Patrol radius (blue, from spawn)
            Vector3 origin = Application.isPlaying ? spawnPosition : transform.position;
            Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.1f);
            Gizmos.DrawWireSphere(origin, patrolRadius);

            // Last heard sound position (if active)
            if (Application.isPlaying && lastHeardPosition != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(lastHeardPosition, 0.4f);
                Gizmos.DrawLine(transform.position + Vector3.up, lastHeardPosition + Vector3.up * 0.5f);
            }

            // State label
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.5f,
                $"State: {CurrentState}" +
                (soundsHeardRecently > 0 ? $"  Sounds: {soundsHeardRecently}" : "")
            );
        }
        #endif
    }
}