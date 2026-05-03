using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    [SerializeField] private float delay = 0f;

    private void Start()
    {
        // Add the listener
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.onDeath.AddListener(TriggerGameOver);
    }

    // ADD THIS METHOD:
    private void OnDestroy()
    {
        // Remove the listener so it doesn't cause errors after scene reloads
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.onDeath.RemoveListener(TriggerGameOver);
    }

    private void TriggerGameOver()
    {
        Invoke(nameof(LoadGameOverScene), delay);
    }

    private void LoadGameOverScene()
    {
        // Unfreeze time just in case the player died while time was slowed/stopped
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene("GameOver");
    }
}