using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] GameObject pauseMenuUI;

    [Tooltip("If pauseMenuUI gets lost (e.g. after a scene reload), try to find a GameObject with this name in the scene. Leave default unless your panel has a different name.")]
    [SerializeField] string pauseMenuUIName = "PauseMenuUI";

    bool isPaused = false;

    // FIXED: re-find the UI reference whenever a scene loads, in case this
    // PauseMenu is on a DontDestroyOnLoad object and survived the reload.
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reset pause state every time a scene loads
        isPaused = false;
        Time.timeScale = 1f;

        // If our reference died with the old scene, find the new panel
        TryFindPauseMenuUI();

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);
    }

    void Start()
    {
        TryFindPauseMenuUI();
        ResumeGame();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        // FIXED: null-safe so a missing/destroyed reference doesn't throw.
        if (!EnsurePauseMenuUI()) return;

        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        if (EnsurePauseMenuUI())
            pauseMenuUI.SetActive(false);

        Time.timeScale = 1f;
        isPaused = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        isPaused = false;

        // Hide the menu BEFORE the scene unloads so we don't end up with
        // a stale reference flicker.
        if (EnsurePauseMenuUI())
            pauseMenuUI.SetActive(false);

        SceneManager.LoadSceneAsync(0);
    }

    public void ExitGame()
    {
        Debug.Log("Exiting game...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Returns true if pauseMenuUI is currently usable.
    // If it's been destroyed (Unity's "fake null"), tries to recover.
    bool EnsurePauseMenuUI()
    {
        if (pauseMenuUI == null)
            TryFindPauseMenuUI();

        if (pauseMenuUI == null)
        {
            Debug.LogWarning($"[PauseMenu] pauseMenuUI is missing and could not be found by name '{pauseMenuUIName}'.", this);
            return false;
        }
        return true;
    }

    void TryFindPauseMenuUI()
    {
        if (pauseMenuUI != null) return;
        if (string.IsNullOrEmpty(pauseMenuUIName)) return;

        // transform.Find looks at children and DOES find inactive objects
        Transform found = transform.Find(pauseMenuUIName);

        if (found != null)
        {
            pauseMenuUI = found.gameObject;
            Debug.Log($"[PauseMenu] Auto-recovered pauseMenuUI reference from children.");
        }
    }
}