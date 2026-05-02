using UnityEngine;

public class ChestInteract : MonoBehaviour
{
    [Header("Keys")]
    [SerializeField] private KeyCode key1 = KeyCode.E;
    [SerializeField] private KeyCode key2 = KeyCode.Space;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string openTriggerName = "Open";

    [Header("ID (must be unique)")]
    public string chestId = "Chest_01";

    private bool playerInRange;
    private bool hasOpened;

    public bool IsOpen => hasOpened;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void Start()
    {
        if (ChestManager.Instance != null)
            ChestManager.Instance.RegisterChest(this);
    }

    private void Update()
    {
        if (!playerInRange) return;
        if (hasOpened) return;

        if (Input.GetKeyDown(key1) || Input.GetKeyDown(key2))
        {
            hasOpened = true;
            animator.SetTrigger(openTriggerName);

            // Inform manager
            if (ChestManager.Instance != null)
                ChestManager.Instance.MarkOpened(this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = false;
    }
}