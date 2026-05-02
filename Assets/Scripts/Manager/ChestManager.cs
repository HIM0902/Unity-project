using System.Collections.Generic;
using UnityEngine;

public class ChestManager : MonoBehaviour
{
    public static ChestManager Instance { get; private set; }

    [Header("Hint Key")]
    [SerializeField] private KeyCode hintKey = KeyCode.H;

    [Header("Indicator Reference")]
    [SerializeField] private OnscreenIndicator indicator;

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

        // Optional: if we were pointing at this chest, hide the indicator
        if (indicator != null && indicator.IsShowing && indicator.CurrentTarget == chest.transform)
        {
            indicator.Hide();
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(hintKey)) return;

        ChestInteract next = GetNextUnopenedChest();

        if (next == null)
        {
            Debug.Log("All chests opened!");
            if (indicator != null) indicator.Hide();
            return;
        }

        if (indicator != null)
        {
            // Toggle behavior: pressing H again hides if already pointing at the next chest
            if (indicator.IsShowing && indicator.CurrentTarget == next.transform)
            {
                indicator.Hide();
            }
            else
            {
                indicator.ShowForTarget(next.transform);
            }
        }

        Debug.Log("Next chest: " + next.chestId);
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