using UnityEngine;
using UnityEngine.Events;

public class ScoreManager : MonoBehaviour
{
    [Header("Score Settings")]
    public int currentScore = 0;
    public int killStreakCount = 0;
    public int killStreakMultiplier = 1;
    public int killStreakThreshold = 3;

    [Header("Wave Settings")]
    public int currentWave = 1;
    public int enemiesRemainingInWave = 0;

    [Header("Events")]
    public UnityEvent onScoreChanged;
    public UnityEvent onWaveChanged;
    public UnityEvent onKillStreakChanged;

    public static ScoreManager Instance { get; private set; }

    private int killsSinceLastStreak = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RegisterKill(int baseScore = 100)
    {
        killsSinceLastStreak++;
        killStreakCount++;

        if (killsSinceLastStreak >= killStreakThreshold)
        {
            killStreakMultiplier++;
            killsSinceLastStreak = 0;
            onKillStreakChanged?.Invoke();
        }

        currentScore += baseScore * killStreakMultiplier;

        if (enemiesRemainingInWave > 0)
            enemiesRemainingInWave--;

        onScoreChanged?.Invoke();
    }

    public void ResetStreak()
    {
        killStreakCount = 0;
        killStreakMultiplier = 1;
        killsSinceLastStreak = 0;
        onKillStreakChanged?.Invoke();
    }

    public void StartWave(int enemyCount)
    {
        currentWave++;
        enemiesRemainingInWave = enemyCount;
        onWaveChanged?.Invoke();
        Debug.Log($"Wave {currentWave} started with {enemyCount} enemies.");
    }

    public string GetFormattedScore() => currentScore.ToString("N0").PadLeft(7, '0');

    public string GetStreakLabel() =>
        killStreakMultiplier <= 1 ? "" : $"x{killStreakMultiplier} KILL STREAK";
}