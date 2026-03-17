using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float damage = 10f;
    public float lifetime = 5f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Bullet hit: " + collision.gameObject.name);
        
        Health health = collision.gameObject.GetComponentInParent<Health>();
        if (health != null)
            health.TakeDamage(damage);
        else
            Debug.Log("No Health component found on: " + collision.gameObject.name);

        Destroy(gameObject);
    }
}
