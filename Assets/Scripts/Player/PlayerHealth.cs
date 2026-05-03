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

    [Tooltip("How much of incoming damage armor absorbs (0–1). 1 = armor eats ALL damage until it breaks (recommended). 0.5 = old behavior where armor and health share damage.")]
    [Range(0f, 1f)]
    public float armorAbsorption = 1f;

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
        if (isDead || damage <= 0f) return;

        // STEP 1: Armor soaks up the configured fraction of damage first.
        if (currentArmor > 0f)
        {
            float armorPortion = damage * armorAbsorption;
            float armorAbsorb = Mathf.Min(currentArmor, armorPortion);

            currentArmor -= armorAbsorb;
            damage -= armorAbsorb;
        }

        // STEP 2: Whatever damage is left bleeds into health.
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
        onDamaged?.Invoke();
    }

    public void AddArmor(float amount)
    {
        currentArmor = Mathf.Clamp(currentArmor + amount, 0f, maxArmor);
        onDamaged?.Invoke();
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