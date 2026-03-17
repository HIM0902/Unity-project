using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("Health & Armor")]
    public Slider healthSlider;
    public Slider armorSlider;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI armorText;

    [Header("Ammo")]
    public TextMeshProUGUI ammoCurrentText;
    public TextMeshProUGUI ammoReserveText;
    public TextMeshProUGUI weaponNameText;

    [Header("Score")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI killStreakText;

    [Header("Wave")]
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI enemiesRemainingText;

    [Header("Status Warnings")]
    public GameObject lowAmmoWarning;
    public GameObject infectionWarning;
    public TextMeshProUGUI infectionPercentText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.onDamaged.AddListener(UpdateHealthUI);
            PlayerHealth.Instance.onDeath.AddListener(OnPlayerDeath);
        }

        if (PlayerAmmo.Instance != null)
        {
            PlayerAmmo.Instance.onAmmoChanged.AddListener(UpdateAmmoUI);
            PlayerAmmo.Instance.onOutOfAmmo.AddListener(ShowLowAmmoWarning);
        }

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.onScoreChanged.AddListener(UpdateScoreUI);
            ScoreManager.Instance.onWaveChanged.AddListener(UpdateWaveUI);
            ScoreManager.Instance.onKillStreakChanged.AddListener(UpdateScoreUI);
        }

        UpdateHealthUI();
        UpdateAmmoUI();
        UpdateScoreUI();
        UpdateWaveUI();
    }

    public void UpdateHealthUI()
    {
        if (PlayerHealth.Instance == null) return;

        if (healthSlider != null)
        {
            healthSlider.maxValue = PlayerHealth.Instance.maxHealth;
            healthSlider.value = PlayerHealth.Instance.currentHealth;
        }

        if (armorSlider != null)
        {
            armorSlider.maxValue = PlayerHealth.Instance.maxArmor;
            armorSlider.value = PlayerHealth.Instance.currentArmor;
        }

        if (healthText != null)
            healthText.text = Mathf.RoundToInt(PlayerHealth.Instance.currentHealth).ToString();

        if (armorText != null)
            armorText.text = Mathf.RoundToInt(PlayerHealth.Instance.currentArmor).ToString();
    }

    public void UpdateAmmoUI()
    {
        if (PlayerAmmo.Instance == null) return;

        if (ammoCurrentText != null)
            ammoCurrentText.text = PlayerAmmo.Instance.currentAmmo.ToString("D2");

        if (ammoReserveText != null)
            ammoReserveText.text = PlayerAmmo.Instance.reserveAmmo.ToString();

        if (lowAmmoWarning != null && PlayerAmmo.Instance.currentAmmo > 5)
            lowAmmoWarning.SetActive(false);
    }

    public void ShowLowAmmoWarning()
    {
        if (lowAmmoWarning != null)
            lowAmmoWarning.SetActive(true);
    }

    public void SetWeaponName(string name)
    {
        if (weaponNameText != null)
            weaponNameText.text = name.ToUpper();
    }

    public void UpdateScoreUI()
    {
        if (ScoreManager.Instance == null) return;

        if (scoreText != null)
            scoreText.text = ScoreManager.Instance.GetFormattedScore();

        if (killStreakText != null)
        {
            string streak = ScoreManager.Instance.GetStreakLabel();
            killStreakText.text = streak;
            killStreakText.gameObject.SetActive(streak.Length > 0);
        }
    }

    public void UpdateWaveUI()
    {
        if (ScoreManager.Instance == null) return;

        if (waveText != null)
            waveText.text = ScoreManager.Instance.currentWave.ToString("D2");

        if (enemiesRemainingText != null)
            enemiesRemainingText.text = ScoreManager.Instance.enemiesRemainingInWave.ToString();
    }

    public void UpdateInfection(float percent)
    {
        if (infectionWarning != null)
            infectionWarning.SetActive(percent > 0f);

        if (infectionPercentText != null)
            infectionPercentText.text = $"Infection {Mathf.RoundToInt(percent)}%";
    }

    private void OnPlayerDeath()
    {
        gameObject.SetActive(false);
        Debug.Log("Player died — HUD hidden.");
    }
}