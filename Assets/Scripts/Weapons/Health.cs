using UnityEngine;
using ZombieAI;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    private float currentHealth;
    private bool isDead = false;

    private ZombieAnimatorBridge animatorBridge;

    void Start()
    {
        currentHealth = maxHealth;
        animatorBridge = GetComponent<ZombieAnimatorBridge>();
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            isDead = true;
            Die();
        }
        else
        {
            animatorBridge?.TriggerHit();
        }
    }

    void Die()
    {
        GetComponent<ZombieAIController>()?.TransitionTo(ZombieState.Dead);

        WaveSpawner spawner = FindObjectOfType<WaveSpawner>();
        if (spawner != null)
            spawner.ZombieKilled();

        Destroy(gameObject, 2f);
    }
}