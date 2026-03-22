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
        private static readonly int SpeedHash      = Animator.StringToHash("Speed");
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
            ai.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            ai.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            // Blend locomotion speed
            float speed = agent.velocity.magnitude / agent.speed;
            animator.SetFloat(SpeedHash, speed, 0.1f, Time.deltaTime);
        }

        private void HandleStateChanged(ZombieState from, ZombieState to)
        {
            animator.SetInteger(StateHash, (int)to);
            animator.SetBool(IsAttackingHash, to == ZombieState.Attack);

            if (to == ZombieState.Attack)
            {
                animator.SetTrigger(AttackHash);
            }
        }
    }
}
