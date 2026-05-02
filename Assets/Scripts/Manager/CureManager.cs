using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CureManager : MonoBehaviour
{
    public static CureManager Instance { get; private set; }

    [Header("Cure Settings")]
    [SerializeField] private int totalParts = 5;

    [Header("Events")]
    public UnityEvent onPartCollected;
    public UnityEvent onCureComplete;

    private HashSet<string> collectedParts = new HashSet<string>();

    public int PartsCollected => collectedParts.Count;
    public int TotalParts => totalParts;
    public bool IsCureComplete => PartsCollected >= totalParts;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void CollectPart(string partId)
    {
        // Prevent double-counting
        if (IsCureComplete) return;
        if (string.IsNullOrEmpty(partId)) return;

        bool added = collectedParts.Add(partId);
        if (!added) return;

        Debug.Log($"Cure part collected: {partId} ({PartsCollected}/{totalParts})");
        onPartCollected?.Invoke();

        if (PartsCollected >= totalParts)
        {
            Debug.Log("CURE COMPLETE! Player can keep playing for high score.");
            onCureComplete?.Invoke();
        }
    }
}