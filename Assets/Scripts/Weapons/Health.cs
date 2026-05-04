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
    [Tooltip("How long to wait before destroying the zombie after death (seconds). Match your Death clip / death sound length.")]
    [SerializeField] private float destroyDelay = 4f;

    [Tooltip("Minimum time between hit reactions so it doesn't spam Hit1/Hit2 every bullet frame.")]
    [SerializeField] private float hitReactCooldown = 0.25f;

    [Header("Death Fix")]
    [Tooltip("If true, locks the zombie root position after death so it doesn't slide.")]
    [SerializeField] private bool lockPositionAfterDeath = true;

    [Header("Death Audio")]
    [Tooltip("Death sounds played when the zombie dies. One is picked randomly. Optional.")]
    [SerializeField] private AudioClip[] deathSounds;

    // Hardcoded volume so multiple zombie deaths don't deafen the player.
    // Tweak this single number if it's too quiet/loud across your whole game.
    private const float DEATH_VOLUME = 0.05f;
    private const float DEATH_PITCH_RANGE = 0.1f;

    private float currentHealth;
    private bool isDead = false;

    private Animator animator;
    private AudioSource deathAudioSource;

    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int Hit1Hash = Animator.StringToHash("Hit1");
    private static readonly int Hit2Hash = Animator.StringToHash("Hit2");

    private float hitReactTimer = 0f;

    private Vector3 deathPosition;

    void Start()
    {
        currentHealth = maxHealth;

        animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("Health: No Animator found in children of " + gameObject.name +
                             ". Hit/Death animations will not play.");
        }
    }

    void Update()
    {
        if (hitReactTimer > 0f)
            hitReactTimer -= Time.deltaTime;
    }

    private void LateUpdate()
    {
        if (!isDead) return;
        if (!lockPositionAfterDeath) return;

        transform.position = deathPosition;
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        SpawnHitBlood();

        currentHealth -= damage;

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

        deathPosition = transform.position;

        if (deathBloodPrefab != null)
        {
            Instantiate(
                deathBloodPrefab,
                transform.position + Vector3.up,
                Quaternion.identity
            );
        }

        if (animator != null)
        {
            animator.SetBool(IsDeadHash, true);
        }

        PlayDeathSound();

        DisableZombieGameplayComponents();

        WaveSpawner spawner = FindObjectOfType<WaveSpawner>();
        if (spawner != null)
        {
            spawner.ZombieKilled();
        }

        StartCoroutine(DestroyAfterDelay());
    }

    // FIXED: volume is now hardcoded to DEATH_VOLUME (0.05f).
    // Single source of truth — no per-zombie tuning needed.
    private void PlayDeathSound()
    {
        if (deathSounds == null || deathSounds.Length == 0) return;

        AudioClip clip = deathSounds[Random.Range(0, deathSounds.Length)];
        if (clip == null) return;

        deathAudioSource = gameObject.AddComponent<AudioSource>();
        deathAudioSource.playOnAwake = false;
        deathAudioSource.spatialBlend = 1f;
        deathAudioSource.minDistance = 2f;
        deathAudioSource.maxDistance = 25f;
        deathAudioSource.rolloffMode = AudioRolloffMode.Linear;
        deathAudioSource.pitch = 1f + Random.Range(-DEATH_PITCH_RANGE, DEATH_PITCH_RANGE);
        deathAudioSource.PlayOneShot(clip, DEATH_VOLUME);
    }

    private void DisableZombieGameplayComponents()
    {
        var ai = GetComponent<ZombieAI.ZombieAIController>();
        if (ai != null) ai.enabled = false;

        var animBridge = GetComponent<ZombieAI.ZombieAnimatorBridge>();
        if (animBridge != null) animBridge.enabled = false;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }

        Collider[] cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols)
        {
            c.enabled = false;
        }

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