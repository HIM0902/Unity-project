using UnityEngine;
using UnityEngine.AI;

namespace ZombieAI
{
    [RequireComponent(typeof(ZombieAIController))]
    [RequireComponent(typeof(Animator))]
    public class ZombieAnimatorBridge : MonoBehaviour
    {
        private ZombieAIController ai;
        private Animator animator;
        private NavMeshAgent agent;

        // Cached parameter hashes for performance
        private static readonly int SpeedHash       = Animator.StringToHash("Speed");
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
        private static readonly int AttackHash      = Animator.StringToHash("Attack");
        private static readonly int StateHash       = Animator.StringToHash("State");

        private void Awake()
        {
            ai       = GetComponent<ZombieAIController>();
            animator = GetComponent<Animator>();
            agent    = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            ai.OnStateChanged    += HandleStateChanged;
            ai.OnAttackPerformed += HandleAttackPerformed; // NEW: re-trigger anim on every swing
        }

        private void OnDisable()
        {
            ai.OnStateChanged    -= HandleStateChanged;
            ai.OnAttackPerformed -= HandleAttackPerformed;
        }

        private void Update()
        {
            float normalizedSpeed = 0f;

            // If agent.speed is 0 (in melee range during Attack), don't divide.
            if (agent != null && agent.speed > 0.001f)
            {
                normalizedSpeed = agent.velocity.magnitude / agent.speed;
            }

            // Extra safety: if anything goes weird, force it back to 0.
            if (float.IsNaN(normalizedSpeed) || float.IsInfinity(normalizedSpeed))
                normalizedSpeed = 0f;

            animator.SetFloat(SpeedHash, normalizedSpeed, 0.1f, Time.deltaTime);
        }

        private void HandleStateChanged(ZombieState from, ZombieState to)
        {
            animator.SetInteger(StateHash, (int)to);
            animator.SetBool(IsAttackingHash, to == ZombieState.Attack);

            // NOTE: we no longer fire AttackHash here — that's handled by
            // HandleAttackPerformed so every swing re-plays the kick animation,
            // not just the first one when entering the Attack state.
        }

        // NEW: called by ZombieAIController.OnAttackPerformed every time PerformAttack() runs.
        // This is what makes the kick animation replay on every swing instead of only once.
        private void HandleAttackPerformed()
        {
            animator.ResetTrigger(AttackHash); // clear any stale trigger first
            animator.SetTrigger(AttackHash);
        }
    }
}