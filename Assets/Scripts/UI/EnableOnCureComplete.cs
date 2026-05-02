using UnityEngine;

public class EnableOnCureComplete : MonoBehaviour
{
    [SerializeField] private GameObject helipadParent;

    private void Start()
    {
        if (helipadParent == null)
        {
            Debug.LogWarning("Helipad parent not assigned.");
            return;
        }

        // If the cure is already complete, enable immediately
        if (CureManager.Instance != null && CureManager.Instance.IsCureComplete)
        {
            helipadParent.SetActive(true);
            return;
        }

        // Otherwise wait for the cure complete event
        if (CureManager.Instance != null)
        {
            CureManager.Instance.onCureComplete.AddListener(EnableHelipad);
        }
        else
        {
            Debug.LogWarning("CureManager.Instance not found in scene.");
        }
    }

    private void OnDestroy()
    {
        if (CureManager.Instance != null)
        {
            CureManager.Instance.onCureComplete.RemoveListener(EnableHelipad);
        }
    }

    private void EnableHelipad()
    {
        helipadParent.SetActive(true);
        Debug.Log("Helipad enabled (cure complete).");
    }
}