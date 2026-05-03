using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;

    [Header("Blood Effects")]
    public GameObject bloodHitPrefab;
    public GameObject deathBloodPrefab;

    [Header("Animation")]
    [Tooltip("How long to wait before destroying the zombie after death (seconds). Match your Death clip length.")]
    [SerializeField] private float destroyDelay = 3f;

    [Tooltip("Minimum time between hit reactions so it doesn't spam Hit1/Hit2 every bullet frame.")]
    [SerializeField] private float hitReactCooldown = 0.25f;

    [Header("Death Fix")]
    [Tooltip("If true, locks the zombie root position after death so it doesn't slide.")]
    [SerializeField] private bool lockPositionAfterDeath = true;

    private float currentHealth;
    private bool isDead = false;

    // Animator lives on child joints, so we must find it in children
    private Animator animator;

    // Cached parameter hashes (avoids typos + faster)
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int Hit1Hash = Animator.StringToHash("Hit1");
    private static readonly int Hit2Hash = Animator.StringToHash("Hit2");

    private float hitReactTimer = 0f;

    // Used to stop the root from sliding after death
    private Vector3 deathPosition;

    void Start()
    {
        currentHealth = maxHealth;

        // IMPORTANT: Animator is on joints child, so use GetComponentInChildren
        animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("Health: No Animator found in children of " + gameObject.name +
                             ". Hit/Death animations will not play.");
        }
    }

    void Update()
    {
        // Cooldown timer for hit reactions
        if (hitReactTimer > 0f)
            hitReactTimer -= Time.deltaTime;
    }

    // Runs after Update, great place to "win" the final transform position each frame
    private void LateUpdate()
    {
        // If dead, keep the root pinned so it doesn't slide
        if (!isDead) return;
        if (!lockPositionAfterDeath) return;

        transform.position = deathPosition;
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        SpawnHitBlood();

        currentHealth -= damage;

        // Play a hit reaction (random Hit1 / Hit2), but don't spam it every frame
        TryPlayHitReaction();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void TryPlayHitReaction()
    {
        if (animator == null) return;
        if (hitReactTimer > 0f) return;

        hitReactTimer = hitReactCooldown;

        // Randomly pick Hit1 or Hit2
        if (Random.value < 0.5f)
            animator.SetTrigger(Hit1Hash);
        else
            animator.SetTrigger(Hit2Hash);
    }

    private void SpawnHitBlood()
    {
        if (bloodHitPrefab != null)
        {
            Instantiate(
                bloodHitPrefab,
                transform.position + Vector3.up * 1.2f,
                Quaternion.identity
            );
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // Cache death position so we can lock it (prevents sliding)
        deathPosition = transform.position;

        // Spawn death blood
        if (deathBloodPrefab != null)
        {
            Instantiate(
                deathBloodPrefab,
                transform.position + Vector3.up,
                Quaternion.identity
            );
        }

        // Tell animator to play death
        if (animator != null)
        {
            animator.SetBool(IsDeadHash, true);
        }

        // Stop AI + movement so death animation can play cleanly
        DisableZombieGameplayComponents();

        // Notify wave spawner
        WaveSpawner spawner = FindObjectOfType<WaveSpawner>();
        if (spawner != null)
        {
            spawner.ZombieKilled();
        }

        // Destroy after animation time
        StartCoroutine(DestroyAfterDelay());
    }

    private void DisableZombieGameplayComponents()
    {
        // Disable AI scripts if present
        var ai = GetComponent<ZombieAI.ZombieAIController>();
        if (ai != null) ai.enabled = false;

        var animBridge = GetComponent<ZombieAI.ZombieAnimatorBridge>();
        if (animBridge != null) animBridge.enabled = false;

        // Stop NavMeshAgent
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }

        // If you have any "root motion extraction / follow" scripts, disable them here too
        // Example (only if it exists in your project):
        // var extractor = GetComponent<ExtractRootMotionToParent>();
        // if (extractor != null) extractor.enabled = false;

        // Disable colliders so bullets don't keep hitting / player doesn't get blocked weirdly
        Collider[] cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols)
        {
            c.enabled = false;
        }

        // Optional: disable rigidbody momentum if present
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }
}