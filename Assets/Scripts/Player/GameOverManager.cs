using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    [SerializeField] private float delay = 0f;

    private void Start()
    {
        PlayerHealth.Instance.onDeath.AddListener(TriggerGameOver);
    }

    private void TriggerGameOver()
    {
        Invoke(nameof(LoadGameOverScene), delay);
    }

    private void LoadGameOverScene()
    {
        // Enable cursor for menu scenes
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene("GameOver");
    }
}