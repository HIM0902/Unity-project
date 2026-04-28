using UnityEngine;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;
    private bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        // If the zombie is already dead, ignore any extra bullets!
        if (isDead) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            // 1. Lock it! The zombie is now officially dead.
            isDead = true;

            // 2. Tell the spawner
            WaveSpawner spawner = FindObjectOfType<WaveSpawner>();
            if (spawner != null)
            {
                spawner.ZombieKilled();
            }

            // 3. Destroy the zombie (or play death animation)
            Destroy(gameObject);
        }
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} died.");
        // Replace with your own death logic (animation, destroy, respawn, etc.)

        WaveSpawner spawner = FindObjectOfType<WaveSpawner>();
        if (spawner != null)
        {
            spawner.ZombieKilled();
        }
        Destroy(gameObject);
    }
}
