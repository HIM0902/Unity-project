using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ExtractionPoint : MonoBehaviour
{
    [Header("Extraction Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool requireCureComplete = true;

    [Header("High Score Panel")]
    [Tooltip("The root HighScorePanel GameObject — this is what gets shown/hidden.")]
    [SerializeField] private GameObject highScorePanelRoot;

    [Tooltip("The HighScoreUI component sitting on the HighScore child GameObject.")]
    [SerializeField] private HighScoreUI highScoreUI;

    [Tooltip("The CurrentScore TMP text in the panel.")]
    [SerializeField] private TextMeshProUGUI currentScoreText;

    private bool extractionEnabled = false;
    private bool hasExtracted      = false;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        extractionEnabled = !requireCureComplete;

        if (requireCureComplete && CureManager.Instance != null)
        {
            if (CureManager.Instance.IsCureComplete)
                extractionEnabled = true;
            else
                CureManager.Instance.onCureComplete.AddListener(EnableExtraction);
        }
    }

    private void OnDestroy()
    {
        if (CureManager.Instance != null)
            CureManager.Instance.onCureComplete.RemoveListener(EnableExtraction);
    }

    // -----------------------------------------------------------------------
    // Update — debug shortcut
    // -----------------------------------------------------------------------

    private void Update()
    {
        // Press F10 to instantly trigger extraction (REMOVE BEFORE RELEASE)
        if (Input.GetKeyDown(KeyCode.F10))
        {
            Debug.Log("DEBUG: Forced extraction via F10.");
            extractionEnabled = true;
            TriggerExtraction();
        }
    }

    // -----------------------------------------------------------------------
    // Extraction logic
    // -----------------------------------------------------------------------

    private void EnableExtraction()
    {
        extractionEnabled = true;
        Debug.Log("Extraction enabled!");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!extractionEnabled) return;
        if (!other.CompareTag(playerTag)) return;
        if (hasExtracted) return;

        TriggerExtraction();
    }

    private void TriggerExtraction()
    {
        if (hasExtracted) return;
        hasExtracted = true;
        Debug.Log("Player extracted!");
        Time.timeScale = 0f;

        // 1. Save the score and get the rank it landed at
        int rank = RecordScore();

        // 2. Display the current run's score at the top of the panel
        if (currentScoreText != null)
            currentScoreText.text = "Current Score: " + GetCurrentScore();

        // 3. Show the root panel then tell HighScoreUI to populate and highlight
        if (highScorePanelRoot != null)
            highScorePanelRoot.SetActive(true);

        if (highScoreUI != null)
            highScoreUI.Show(rank);
        else
            Debug.LogWarning("ExtractionPoint: No HighScoreUI assigned – skipping panel.");
    }

    // -----------------------------------------------------------------------
    // Score recording
    // -----------------------------------------------------------------------

    private int RecordScore()
    {
        if (HighScoreManager.Instance == null)
        {
            Debug.LogWarning("ExtractionPoint: No HighScoreManager in scene – score not saved.");
            return -1;
        }

        int finalScore = GetCurrentScore();

        bool madeBoard = HighScoreManager.Instance.SubmitScore(finalScore);
        int  rank      = madeBoard ? new List<int>(HighScoreManager.Instance.GetTopScores()).IndexOf(finalScore) : -1;

        Debug.Log($"Score {finalScore} submitted. Made leaderboard: {madeBoard}" +
                  (madeBoard ? $" at rank {rank + 1}." : "."));

        return rank;
    }

    private int GetCurrentScore()
    {
        WaveSpawner spawner = FindObjectOfType<WaveSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("ExtractionPoint: No WaveSpawner found in scene.");
            return 0;
        }
        return spawner.currentScore;
    }
}