using UnityEngine;

public class Gun : MonoBehaviour
{
    public Camera fpsCamera;
    public GameObject bulletPrefab;
    public Transform firePoint;
    public WeaponData weaponData;

    [Header("Overrides")]
    public AudioClip shootSoundOverride;
    [Range(0f, 1f)] public float volumeOverride = 1f;
    public GameObject muzzleFlashOverride;

    private AudioSource audioSource;
    private float nextFireTime = 0f;
    private int currentAmmo;
    private bool isReloading = false;

    void Start()
    {
        if (fpsCamera == null)
            fpsCamera = Camera.main;

        if (fpsCamera == null)
            Debug.LogError("No camera found! Tag your camera as MainCamera.");

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        currentAmmo = weaponData.maxAmmo;
    }

    void Update()
    {
        if (isReloading) return;

        if (currentAmmo <= 0 || Input.GetKeyDown(KeyCode.R))
        {
            StartCoroutine(Reload());
            return;
        }

        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + weaponData.fireRate;
            Shoot();
        }
    }

    void Shoot()
    {
        currentAmmo--;

        // Use override if set, otherwise fall back to WeaponData
        GameObject flashPrefab = muzzleFlashOverride != null ? muzzleFlashOverride : weaponData.muzzleFlashPrefab;
        AudioClip sound = shootSoundOverride != null ? shootSoundOverride : weaponData.shootSound;
        float volume = volumeOverride > 0 ? volumeOverride : weaponData.volume;

        if (flashPrefab != null)
        {
            GameObject flash = Instantiate(flashPrefab, firePoint.position, firePoint.rotation);
            Destroy(flash, 0.05f);
        }

        if (sound != null)
            audioSource.PlayOneShot(sound, volume);

        Ray ray = new Ray(fpsCamera.transform.position, fpsCamera.transform.forward);
        Vector3 targetPoint;

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 1000f))
            targetPoint = hit.point;
        else
            targetPoint = fpsCamera.transform.position + fpsCamera.transform.forward * 1000f;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        if (bullet == null) return;

        Collider[] playerColliders = GetComponentsInParent<Collider>();
        Collider bulletCollider = bullet.GetComponent<Collider>();
        if (bulletCollider != null)
            foreach (Collider col in playerColliders)
                Physics.IgnoreCollision(bulletCollider, col);

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
            bulletScript.damage = weaponData.damage;

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb == null) rb = bullet.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Vector3 direction = (targetPoint - firePoint.position).normalized;
        rb.linearVelocity = direction * weaponData.bulletSpeed;
    }

    System.Collections.IEnumerator Reload()
    {
        isReloading = true;
        Debug.Log("Reloading...");

        yield return new WaitForSeconds(weaponData.reloadTime);

        currentAmmo = weaponData.maxAmmo;
        isReloading = false;
        Debug.Log("Reloaded!");
    }
}
