using System.Collections.Generic;
using UnityEngine;

public class ChestManager : MonoBehaviour
{
    public static ChestManager Instance { get; private set; }

    [Header("Hint Key")]
    [SerializeField] private KeyCode hintKey = KeyCode.H;

    [Header("Indicator Reference")]
    [SerializeField] private OnscreenIndicator indicator;

    [Header("Extraction Target (set this to ExtractionPoint or Helipad marker)")]
    [SerializeField] private Transform extractionTarget;

    private readonly List<ChestInteract> chests = new List<ChestInteract>();
    private readonly HashSet<string> opened = new HashSet<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RegisterChest(ChestInteract chest)
    {
        if (chest == null) return;

        if (!chests.Contains(chest))
            chests.Add(chest);

        // Ensure consistent order: Chest_01, Chest_02, ...
        chests.Sort((a, b) => string.CompareOrdinal(a.chestId, b.chestId));
    }

    public void MarkOpened(ChestInteract chest)
    {
        if (chest == null) return;

        opened.Add(chest.chestId);

        // If we were pointing at this chest, hide the indicator
        if (indicator != null && indicator.IsShowing && indicator.CurrentTarget == chest.transform)
        {
            indicator.Hide();
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(hintKey)) return;

        // After cure is complete, point to extraction
        if (CureManager.Instance != null && CureManager.Instance.IsCureComplete)
        {
            ShowOrToggleTarget(extractionTarget, "Extraction");
            return;
        }

        // Otherwise, point to the next unopened chest
        ChestInteract next = GetNextUnopenedChest();

        if (next == null)
        {
            Debug.Log("All chests opened!");
            if (indicator != null) indicator.Hide();
            return;
        }

        ShowOrToggleTarget(next.transform, "Next chest: " + next.chestId);
    }

    private void ShowOrToggleTarget(Transform target, string debugLabel)
    {
        if (indicator == null)
        {
            Debug.LogWarning("ChestManager: Indicator reference is missing.");
            return;
        }

        if (target == null)
        {
            Debug.LogWarning("ChestManager: Target reference is missing.");
            return;
        }

        // Toggle behavior: pressing H again hides if already pointing at the same target
        if (indicator.IsShowing && indicator.CurrentTarget == target)
        {
            indicator.Hide();
        }
        else
        {
            indicator.ShowForTarget(target);
        }

        Debug.Log(debugLabel);
    }

    private ChestInteract GetNextUnopenedChest()
    {
        for (int i = 0; i < chests.Count; i++)
        {
            ChestInteract c = chests[i];
            if (c == null) continue;

            bool isOpen = c.IsOpen || opened.Contains(c.chestId);
            if (!isOpen)
                return c;
        }
        return null;
    }
}