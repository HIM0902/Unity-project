// ZombieSpawner.cs — Spawn and manage zombie instances at runtime
// Unity 6.3 LTS (6000.3.10f1)
//
// SETUP:
//   1. Create an empty GameObject and attach this script.
//   2. Assign a zombie prefab (must have ZombieAIController + NavMeshAgent).
//   3. Set spawn count and radius.
//   4. Zombies spawn on Start or call SpawnWave() from script / UnityEvent.

using UnityEngine;
using UnityEngine.AI;

namespace ZombieAI
{
    public class ZombieSpawner : MonoBehaviour
    {
        [Header("Spawning")]
        [SerializeField] private GameObject zombiePrefab;
        [SerializeField] private int spawnCount = 5;
        [SerializeField] private float spawnRadius = 20f;
        [SerializeField] private bool spawnOnStart = true;

        [Header("Wave Settings")]
        [Tooltip("Seconds between automatic waves. 0 = no auto-waves.")]
        [SerializeField] private float waveCooldown = 0f;

        private float waveTimer;

        private void Start()
        {
            if (spawnOnStart)
                SpawnWave();
        }

        private void Update()
        {
            if (waveCooldown > 0f)
            {
                waveTimer += Time.deltaTime;
                if (waveTimer >= waveCooldown)
                {
                    SpawnWave();
                    waveTimer = 0f;
                }
            }
        }

        public void SpawnWave()
        {
            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 randomPos = transform.position + Random.insideUnitSphere * spawnRadius;
                randomPos.y = transform.position.y;

                if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, spawnRadius, NavMesh.AllAreas))
                {
                    Instantiate(zombiePrefab, hit.position, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                }
            }
        }

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
        #endif
    }
}
