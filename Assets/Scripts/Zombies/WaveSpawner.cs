using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.AI;

public class WaveSpawner : MonoBehaviour
{
    // This makes the Spawner a "Singleton" so zombies can talk to it easily
    public static WaveSpawner Instance;

    void Awake()
    {
        Instance = this;
    }




    [Header("Dynamic Spawning")]
    public Transform player; // Drag your player character here in Unity
    public float minSpawnDistance = 15f; // Don't spawn closer than this
    public float maxSpawnDistance = 30f; // Don't spawn further than this
    
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
    public float speedBoostPerTier = 0.25f; // NEW: adds 25% extra speed every 5 waves
    
    private int currentWave = 0;
    public int zombiesAlive = 0; 
    public int currentScore = 0; // NEW: Tracks the player's total score
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

        // press F9 to instantly skip 4 waves for testing
        if (Input.GetKeyDown(KeyCode.F9))
        {
            currentWave += 4; 
            Debug.Log("skipped to wave: " + currentWave); //REMOVE THIS BEFORE RELEASE, ITS JUST FOR TESTING
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
        // Safety check: Make sure we have prefabs and the player is assigned!
        if (zombiePrefabs.Length == 0 || player == null) return;

        GameObject randomZombiePrefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Length)];

        // --- THE SAFETY CHECK ---
        if (randomZombiePrefab == null)
        {
            Debug.LogError("WaveSpawner: A zombie prefab is missing! Make sure you dragged Project Prefabs, not Hierarchy objects, into the array.");
            return; // Stop here so we don't crash the game
        }
        // ------------------------

        // --- DYNAMIC SPAWNING MATH ---
        // Use our new method to find a valid spot on the NavMesh around the player
        Vector3 finalSpawnPosition = GetDynamicSpawnPosition();

        // Instantiate the zombie at the new dynamic position
        GameObject spawnedZombie = Instantiate(randomZombiePrefab, finalSpawnPosition, Quaternion.identity);
        
        // Make the zombie look at the player immediately so they don't spawn facing a wall
        spawnedZombie.transform.LookAt(new Vector3(player.position.x, spawnedZombie.transform.position.y, player.position.z));

        // --- DYNAMIC DIFFICULTY & SPEED SCALING ---
        int speedTier = currentWave / 5;
        speedTier = Mathf.Clamp(speedTier, 0, 3); 
        float currentSpeedMultiplier = 1f + (speedBoostPerTier * speedTier);

        // --- THE FIX ---
        // Use GetComponentInChildren just in case the script is hiding on a nested object
        ZombieAI.ZombieAIController aiScript = spawnedZombie.GetComponentInChildren<ZombieAI.ZombieAIController>();
        
        if (aiScript != null)
        {
            aiScript.ApplySpeedMultiplier(currentSpeedMultiplier);
        }
        else
        {
            // If it STILL can't find it, it will scream at you in red text
            Debug.LogError("BROKEN LINK: The Spawner couldn't find the ZombieAIController on the prefab!");
        }

        zombiesAlive++;
        UpdateUI();
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

    public Vector3 GetDynamicSpawnPosition()
    {
        // 1. Pick a completely random direction (X and Z axis)
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        
        // 2. Pick a random distance between your min and max limits
        float randomDistance = Random.Range(minSpawnDistance, maxSpawnDistance);
        
        // 3. Calculate the exact 3D point around the player
        Vector3 spawnPoint = player.position + new Vector3(randomDirection.x, 0, randomDirection.y) * randomDistance;

        // 4. Ask the NavMesh to snap this point to the nearest valid ground
        NavMeshHit hit;
        if (NavMesh.SamplePosition(spawnPoint, out hit, 5.0f, NavMesh.AllAreas))
        {
            return hit.position; // Perfect valid spot!
        }

        // Failsafe: If the math somehow picks the sky or the void, just spawn them near the player
        return player.position + (Vector3.forward * minSpawnDistance);
    }
}