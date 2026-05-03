using UnityEngine;

public class MainMenuCleaner : MonoBehaviour
{
    void Start()
    {
        // 1. Reset time scale just in case
        Time.timeScale = 1f;

        // 2. Destroy lingering managers (Replace these strings with the names of your manager GameObjects)
        DestroyGhostObject("GameManager");
        DestroyGhostObject("WaveManager");
        DestroyGhostObject("Player"); // Or whatever your player object is named
        DestroyGhostObject("SpawnManager");
    }

    // Helper method to find and destroy objects by name
    void DestroyGhostObject(string objectName)
    {
        GameObject ghost = GameObject.Find(objectName);
        if (ghost != null)
        {
            Destroy(ghost);
            Debug.Log($"[MainMenuCleaner] Destroyed leftover {objectName}");
        }
    }
}