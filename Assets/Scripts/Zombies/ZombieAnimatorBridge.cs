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

        private static readonly int SpeedHash        = Animator.StringToHash("Speed");
        private static readonly int IsAttackingHash  = Animator.StringToHash("IsAttacking");
        private static readonly int AttackHash       = Animator.StringToHash("Attack");
        private static readonly int StateHash        = Animator.StringToHash("State");
        private static readonly int IsDeadHash       = Animator.StringToHash("IsDead");
        private static readonly int Hit1Hash         = Animator.StringToHash("Hit1");
        private static readonly int Hit2Hash         = Animator.StringToHash("Hit2");

        private void Awake()
        {
            ai       = GetComponent<ZombieAIController>();
            animator = GetComponent<Animator>();
            agent    = GetComponent<NavMeshAgent>();
        }

        private void OnEnable()
        {
            ai.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            ai.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            float normalizedSpeed = 0f;

            if (agent != null && agent.speed > 0.001f)
                normalizedSpeed = agent.velocity.magnitude / agent.speed;

            if (float.IsNaN(normalizedSpeed) || float.IsInfinity(normalizedSpeed))
                normalizedSpeed = 0f;

            animator.SetFloat(SpeedHash, normalizedSpeed, 0.1f, Time.deltaTime);
        }

        private void HandleStateChanged(ZombieState from, ZombieState to)
        {
            animator.SetInteger(StateHash, (int)to);
            animator.SetBool(IsAttackingHash, to == ZombieState.Attack);
            animator.SetBool(IsDeadHash, to == ZombieState.Dead);

            if (to == ZombieState.Attack)
                animator.SetTrigger(AttackHash);
        }

        public void TriggerHit()
        {
            int hitHash = Random.Range(0, 2) == 0 ? Hit1Hash : Hit2Hash;
            animator.SetTrigger(hitHash);
        }
    }
}