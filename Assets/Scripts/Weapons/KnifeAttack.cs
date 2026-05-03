using UnityEngine;

public class KnifeAttack : MonoBehaviour
{
    private Animator animator;

    [Header("Attack Settings")]
    public Camera playerCamera;
    public float attackRange = 2f;
    public float damage = 25f;
    public float backstabDamage = 100f;
    public float attackCooldown = 0.7f;

    [Header("Backstab")]
    [Tooltip("How directly behind the target you need to be. 60° works well — strict enough to feel earned, loose enough to not feel finicky.")]
    [Range(0f, 180f)] public float backstabAngle = 60f;

    [Header("Audio")]
    public AudioClip hitSound;
    public AudioClip missSound;
    public AudioClip backstabSound;
    [Range(0f, 1f)] public float hitVolume = 0.7f;
    [Range(0f, 1f)] public float missVolume = 0.4f;
    [Range(0f, 1f)] public float backstabVolume = 1f;

    public AudioSource audioSource;

    private bool canAttack = true;

    void Awake()
    {
        animator = GetComponent<Animator>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
    }

    void OnEnable()
    {
        canAttack = true;
        if (animator != null)
            animator.ResetTrigger("Attack");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && canAttack)
            Attack();
    }

    void Attack()
    {
        canAttack = false;

        if (animator != null)
            animator.SetTrigger("Attack");

        bool didDamage = false;
        bool wasBackstab = false;

        if (playerCamera != null)
        {
            RaycastHit hit;
            if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, attackRange))
            {
                Health health = hit.collider.GetComponentInParent<Health>();
                if (health != null)
                {
                    // Determine if we're behind the target
                    wasBackstab = IsBackstab(health.transform);

                    float dmg = wasBackstab ? backstabDamage : damage;
                    health.TakeDamage(dmg);
                    didDamage = true;

                    if (wasBackstab)
                        Debug.Log($"[KnifeAttack] BACKSTAB! Dealt {dmg} damage to '{health.gameObject.name}'.");
                }
            }
        }

        PlayAttackSound(didDamage, wasBackstab);

        Invoke(nameof(ResetAttack), attackCooldown);
    }

    bool IsBackstab(Transform target)
    {
        // Vector from target to player camera, flattened to horizontal plane
        Vector3 toAttacker = playerCamera.transform.position - target.position;
        toAttacker.y = 0f;

        Vector3 targetForward = target.forward;
        targetForward.y = 0f;

        if (toAttacker.sqrMagnitude < 0.0001f || targetForward.sqrMagnitude < 0.0001f)
            return false;

        // Angle between target's back (-forward) and the direction to the attacker
        float angle = Vector3.Angle(-targetForward, toAttacker);
        return angle <= backstabAngle * 0.5f;
    }

    void PlayAttackSound(bool didHit, bool wasBackstab)
    {
        if (audioSource == null) return;

        AudioClip clip;
        float volume;

        if (wasBackstab)
        {
            clip = backstabSound != null ? backstabSound : hitSound;
            volume = backstabVolume;
        }
        else if (didHit)
        {
            clip = hitSound;
            volume = hitVolume;
        }
        else
        {
            clip = missSound;
            volume = missVolume;
        }

        if (clip != null)
            audioSource.PlayOneShot(clip, volume);
    }

    void ResetAttack()
    {
        canAttack = true;
    }
}