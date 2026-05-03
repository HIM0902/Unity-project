using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Singleton that persists the top 5 high scores across sessions using PlayerPrefs.
/// Call HighScoreManager.Instance.SubmitScore(score) to record a new run.
/// Fires OnScoreSubmitted whenever a score is added so the UI can react immediately.
/// </summary>
public class HighScoreManager : MonoBehaviour
{
    public static HighScoreManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int maxScores = 5;

    /// <summary>
    /// Fired after every SubmitScore call.
    /// int arg = the 0-based rank the new score landed at, or -1 if it didn't make the board.
    /// </summary>
    public UnityEvent<int> OnScoreSubmitted = new UnityEvent<int>();

    // PlayerPrefs keys
    private const string KEY_PREFIX = "HighScore_";
    private const string COUNT_KEY  = "HighScore_Count";

    // Sorted descending (highest first)
    private List<int> _scores = new List<int>();

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadScores();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns a read-only list of top scores, highest first.
    /// Always returns exactly <see cref="maxScores"/> entries; empty slots are 0.
    /// </summary>
    public IReadOnlyList<int> GetTopScores()
    {
        var result = new List<int>(_scores);
        while (result.Count < maxScores)
            result.Add(0);
        return result.AsReadOnly();
    }

    /// <summary>
    /// Submit a new score. Inserts it into the sorted list if it qualifies,
    /// saves to PlayerPrefs, then fires <see cref="OnScoreSubmitted"/> with the
    /// 0-based rank it landed at (or -1 if it didn't make the board).
    /// </summary>
    public bool SubmitScore(int score)
    {
        _scores.Add(score);
        _scores.Sort((a, b) => b.CompareTo(a)); // descending

        if (_scores.Count > maxScores)
            _scores.RemoveRange(maxScores, _scores.Count - maxScores);

        SaveScores();

        int rank = _scores.IndexOf(score); // -1 if it was kicked out above
        bool madeBoard = rank >= 0;

        OnScoreSubmitted.Invoke(rank);

        Debug.Log(madeBoard
            ? $"HighScoreManager: Score {score} entered the board at rank {rank + 1}."
            : $"HighScoreManager: Score {score} didn't make the top {maxScores}.");

        return madeBoard;
    }

    /// <summary>Wipe all saved scores.</summary>
    public void ClearScores()
    {
        _scores.Clear();
        PlayerPrefs.DeleteKey(COUNT_KEY);
        for (int i = 0; i < maxScores; i++)
            PlayerPrefs.DeleteKey(KEY_PREFIX + i);
        PlayerPrefs.Save();
        OnScoreSubmitted.Invoke(-1); // tell UI to refresh
    }

    // -----------------------------------------------------------------------
    // Persistence
    // -----------------------------------------------------------------------

    private void SaveScores()
    {
        PlayerPrefs.SetInt(COUNT_KEY, _scores.Count);
        for (int i = 0; i < _scores.Count; i++)
            PlayerPrefs.SetInt(KEY_PREFIX + i, _scores[i]);
        PlayerPrefs.Save();
    }

    private void LoadScores()
    {
        _scores.Clear();
        int count = PlayerPrefs.GetInt(COUNT_KEY, 0);
        for (int i = 0; i < count; i++)
            _scores.Add(PlayerPrefs.GetInt(KEY_PREFIX + i, 0));
        _scores.Sort((a, b) => b.CompareTo(a));
    }
}