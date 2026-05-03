using UnityEngine;

/// <summary>
/// Hides this GameObject on Awake so it doesn't show in the Editor preview.
/// Attach to HighScorePanel.
/// </summary>
public class HideOnStart : MonoBehaviour
{
    private void Awake()
    {
        gameObject.SetActive(false);
    }
}