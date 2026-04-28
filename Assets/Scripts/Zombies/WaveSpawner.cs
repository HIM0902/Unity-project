using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.AI;

public class WaveSpawner : MonoBehaviour
{
    [Header("Wave Settings")]
    public GameObject[] zombiePrefabs;
    public Transform[] spawnPoints;
    public float timeBetweenWaves = 5f;
    public float timeBetweenSpawns = 1f;
    
    [Header("UI Settings")]
    public TextMeshProUGUI waveTextUI;
    public TextMeshProUGUI enemiesTextUI;
    public TextMeshProUGUI scoreTextUI; // NEW: Reference to your ScoreText
    
    [Header("Difficulty Scaling")]
    public int baseZombiesPerWave = 3;
    public int additionalZombiesPerWave = 2; 
    public int scorePerKill = 100; // NEW: How many points a zombie is worth
    
    private int currentWave = 0;
    public int zombiesAlive = 0; 
    private int currentScore = 0; // NEW: Tracks the player's total score
    private bool isSpawning = false;

    void Start()
    {
        UpdateUI();
        StartCoroutine(SpawnWave());
    }

    void Update()
    {
        if (zombiesAlive <= 0 && !isSpawning && currentWave > 0)
        {
            StartCoroutine(SpawnWave());
        }
    }

    IEnumerator SpawnWave()
    {
        isSpawning = true;
        zombiesAlive = 0;
        currentWave++;
        
        if (waveTextUI != null)
            waveTextUI.text = "WAVE : " + currentWave;

        if (currentWave > 1)
        {
            yield return new WaitForSeconds(timeBetweenWaves);
        }

        int zombiesToSpawn = baseZombiesPerWave + (additionalZombiesPerWave * (currentWave - 1));

        for (int i = 0; i < zombiesToSpawn; i++)
        {
            SpawnZombie();
            yield return new WaitForSeconds(timeBetweenSpawns);
        }

        isSpawning = false;
    }

    void SpawnZombie()
    {
        if (spawnPoints.Length == 0 || zombiePrefabs.Length == 0) return;

        Transform randomSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject randomZombiePrefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Length)];

        // --- THE SAFETY CHECK ---
        if (randomZombiePrefab == null)
        {
            Debug.LogError("WaveSpawner: A zombie prefab is missing! Make sure you dragged Project Prefabs, not Hierarchy objects, into the array.");
            return; // Stop here so we don't crash the game
        }
        // ------------------------

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomSpawnPoint.position, out hit, 2.0f, NavMesh.AllAreas))
        {
            Instantiate(randomZombiePrefab, hit.position, randomSpawnPoint.rotation);

            zombiesAlive++;
            UpdateUI();
        }
        else
        {
            Debug.LogWarning("Failed to spawn! " + randomSpawnPoint.name + " is too far from a baked NavMesh.");
        }
    }

    // This is called by your Zombie script when it dies
    public void ZombieKilled()
    {
        zombiesAlive--;
        currentScore += scorePerKill; // NEW: Add points to the score
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (enemiesTextUI != null)
        {
            enemiesTextUI.text = "ENEMIES: " + Mathf.Max(0, zombiesAlive);
        }

        // NEW: Update the Score text
        if (scoreTextUI != null)
        {
            scoreTextUI.text = "Score: \n" + currentScore;
        }
    }
}