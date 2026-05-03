using UnityEngine;
using TMPro;

public class CureUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI cureText;

    private void Start()
    {
        UpdateText();

        if (CureManager.Instance != null)
        {
            CureManager.Instance.onPartCollected.AddListener(UpdateText);
            CureManager.Instance.onCureComplete.AddListener(UpdateText);
        }
    }

    private void OnDestroy()
    {
        if (CureManager.Instance != null)
        {
            CureManager.Instance.onPartCollected.RemoveListener(UpdateText);
            CureManager.Instance.onCureComplete.RemoveListener(UpdateText);
        }
    }

    private void UpdateText()
    {
        if (CureManager.Instance == null) return;

        int current = CureManager.Instance.PartsCollected;
        int total = CureManager.Instance.TotalParts;

        if (CureManager.Instance.IsCureComplete)
            cureText.text = $"Cure:\n{total}/{total}\nCOMPLETE";
        else
            cureText.text = $"Cure:\n{current}/{total}";
    }
}