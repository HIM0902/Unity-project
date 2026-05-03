using UnityEngine;

public class CursorManager : MonoBehaviour
{
    void Start()
    {
        // Unlocks the cursor so it can move freely
        Cursor.lockState = CursorLockMode.None;

        // Makes the cursor visible to click UI buttons
        Cursor.visible = true;
    }
}