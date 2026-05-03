using UnityEngine;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;

    [Header("Blood Effects")]
    public GameObject bloodHitPrefab;
    public GameObject deathBloodPrefab;

    private float currentHealth;
    private bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        SpawnHitBlood();

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void SpawnHitBlood()
    {
        if (bloodHitPrefab != null)
        {
            Instantiate(
                bloodHitPrefab,
                transform.position + Vector3.up * 1.2f,
                Quaternion.identity
            );
        }
        else
        {
            Debug.LogWarning("Blood Hit Prefab is not assigned on " + gameObject.name);
        }
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;

        if (deathBloodPrefab != null)
        {
            Instantiate(
                deathBloodPrefab,
                transform.position + Vector3.up,
                Quaternion.identity
            );
        }

        WaveSpawner spawner = FindObjectOfType<WaveSpawner>();
        if (spawner != null)
        {
            spawner.ZombieKilled();
        }

        Destroy(gameObject);
    }
}