using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExtractionPoint : MonoBehaviour
{
    [Header("Extraction Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool requireCureComplete = true;

    [Header("What happens on extract")]
    [SerializeField] private bool loadSceneOnExtract = true;
    [SerializeField] private string sceneToLoad = "GameOver";

    [Header("High Score Panel")]
    [Tooltip("Assign the GameObject that has HighScoreUI on it. " +
             "It starts hidden and is shown here after the score is saved.")]
    [SerializeField] private HighScoreUI highScorePanel;

    private bool extractionEnabled = false;
    private bool hasExtracted      = false; // prevent double-trigger

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
        if (hasExtracted) return; // safety guard against repeated triggers

        hasExtracted = true;
        Debug.Log("Player extracted!");

        // 1. Save the score and get the rank it landed at
        int rank = RecordScore();

        // 2. Show the high score panel with that rank highlighted
        if (highScorePanel != null)
        {
            highScorePanel.Show(rank);
        }
        else
        {
            Debug.LogWarning("ExtractionPoint: No HighScoreUI assigned – skipping panel.");

            // Fall through to scene load if there's no panel to show
            if (loadSceneOnExtract)
                SceneManager.LoadScene(sceneToLoad);
        }

        // If you want the scene to load AFTER the player dismisses the panel,
        // add a "Continue" button that calls SceneManager.LoadScene(sceneToLoad).
        // If you want it to load automatically regardless, uncomment below:
        // if (loadSceneOnExtract) SceneManager.LoadScene(sceneToLoad);
    }

    // -----------------------------------------------------------------------
    // Score recording
    // -----------------------------------------------------------------------

    /// <summary>
    /// Submits the current score to HighScoreManager.
    /// Returns the 0-based rank it landed at, or -1 if it didn't make the board.
    /// </summary>
    private int RecordScore()
    {
        if (HighScoreManager.Instance == null)
        {
            Debug.LogWarning("ExtractionPoint: No HighScoreManager in scene – score not saved.");
            return -1;
        }

        // ── Replace the line below with your own score source ──────────────
        // e.g.  int finalScore = ScoreManager.Instance.CurrentScore;
        int finalScore = GetCurrentScore();
        // ───────────────────────────────────────────────────────────────────

        // SubmitScore now returns the rank directly via the event,
        // but we read it by checking where the score landed.
        bool madeBoard = HighScoreManager.Instance.SubmitScore(finalScore);
        int  rank      = madeBoard ? new List<int>(HighScoreManager.Instance.GetTopScores()).IndexOf(finalScore) : -1;

        Debug.Log($"Score {finalScore} submitted. Made leaderboard: {madeBoard}" +
                  (madeBoard ? $" at rank {rank + 1}." : "."));

        return rank;
    }

    /// <summary>
    /// STUB – replace with your actual score retrieval.
    /// e.g. return ScoreManager.Instance.CurrentScore;
    /// </summary>
    private int GetCurrentScore()
    {
        Debug.LogWarning("ExtractionPoint.GetCurrentScore() is a stub – returning 0. Wire it to your score system.");
        return 0;
    }
}