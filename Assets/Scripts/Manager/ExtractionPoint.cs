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

    private bool extractionEnabled = false;

    private void Start()
    {
        // Start disabled until cure is complete
        extractionEnabled = !requireCureComplete;

        if (requireCureComplete && CureManager.Instance != null)
        {
            // If cure already complete, enable immediately
            if (CureManager.Instance.IsCureComplete)
            {
                extractionEnabled = true;
            }
            else
            {
                CureManager.Instance.onCureComplete.AddListener(EnableExtraction);
            }
        }
    }

    private void OnDestroy()
    {
        if (CureManager.Instance != null)
        {
            CureManager.Instance.onCureComplete.RemoveListener(EnableExtraction);
        }
    }

    private void EnableExtraction()
    {
        extractionEnabled = true;
        Debug.Log("Extraction enabled!");
        // Optional: show UI message here ("Extraction available!")
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!extractionEnabled) return;
        if (!other.CompareTag(playerTag)) return;

        Debug.Log("Player extracted!");

        if (loadSceneOnExtract)
        {
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            // Example: just log for now. Replace with your own UI.
            // You could also pause, show results, etc.
        }
    }
}