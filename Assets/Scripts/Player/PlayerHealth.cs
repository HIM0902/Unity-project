using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Armor Settings")]
    public float maxArmor = 100f;
    public float currentArmor;

    [Header("Events")]
    public UnityEvent onDeath;
    public UnityEvent onDamaged;

    public static PlayerHealth Instance { get; private set; }

    private bool isDead = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize here in Awake so HUDManager can read values in Start
        currentHealth = maxHealth;
        currentArmor = maxArmor;
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        if (currentArmor > 0f)
        {
            float armorAbsorb = Mathf.Min(currentArmor, damage * 0.5f);
            currentArmor -= armorAbsorb;
            damage -= armorAbsorb;
        }

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        onDamaged?.Invoke();

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
    }

    public void AddArmor(float amount)
    {
        currentArmor = Mathf.Clamp(currentArmor + amount, 0f, maxArmor);
    }

    public float GetHealthPercent() => currentHealth / maxHealth;
    public float GetArmorPercent() => currentArmor / maxArmor;

    private void Die()
    {
        isDead = true;
        onDeath?.Invoke();
        Debug.Log("Player died.");
    }
}