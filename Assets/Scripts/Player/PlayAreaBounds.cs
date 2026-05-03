using UnityEngine;

public class PlayAreaBounds : MonoBehaviour
{
    [Header("Where the player goes if they leave")]
    [SerializeField] private Transform returnPoint;

    [Header("Player tag")]
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (returnPoint == null) return;

        // If the player uses a CharacterController, disable it briefly before teleporting
        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            other.transform.position = returnPoint.position;
            other.transform.rotation = returnPoint.rotation;
            cc.enabled = true;
        }
        else
        {
            other.transform.position = returnPoint.position;
            other.transform.rotation = returnPoint.rotation;
        }
    }
}