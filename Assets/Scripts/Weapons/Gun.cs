using UnityEngine;
using ZombieAI;

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

    // FIXED: track whether the gun is properly configured.
    // If weaponData is missing, we log ONCE and disable updates instead of
    // spamming a NullReferenceException every frame.
    private bool isConfigured = false;

    void Start()
    {
        if (fpsCamera == null)
            fpsCamera = Camera.main;

        if (fpsCamera == null)
            Debug.LogError("[Gun] No camera found! Tag your camera as MainCamera.", this);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // FIXED: guard against missing WeaponData.
        if (weaponData == null)
        {
            Debug.LogError($"[Gun] '{gameObject.name}' has no WeaponData assigned! " +
                           "Disabling gun. Drag a WeaponData asset into the inspector, " +
                           "or check whether this gun was instantiated at runtime without one.", this);
            isConfigured = false;
            enabled = false;     // stop Update() from running
            return;
        }

        if (bulletPrefab == null)
        {
            Debug.LogError($"[Gun] '{gameObject.name}' has no Bullet Prefab assigned!", this);
            isConfigured = false;
            enabled = false;
            return;
        }

        if (firePoint == null)
        {
            Debug.LogError($"[Gun] '{gameObject.name}' has no Fire Point assigned!", this);
            isConfigured = false;
            enabled = false;
            return;
        }

        isConfigured = true;
        currentAmmo = weaponData.maxAmmo;
        UpdateHUDAmmo();
    }

    void Update()
    {
        if (!isConfigured) return;
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
        UpdateHUDAmmo();

        // ALERT ZOMBIES — gunshot is loud!
        SoundEmitter.EmitSound(transform.position, 2.5f);

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
        // FIXED: extra safety inside coroutine — if data was lost mid-game,
        // we bail cleanly instead of NRE'ing inside the yield.
        if (weaponData == null)
        {
            isReloading = false;
            yield break;
        }

        isReloading = true;
        Debug.Log("Reloading...");

        if (HUDManager.Instance != null) HUDManager.Instance.ShowReloading();

        yield return new WaitForSeconds(weaponData.reloadTime);

        // Defensive: object may have been destroyed during the wait
        if (this == null || weaponData == null) yield break;

        currentAmmo = weaponData.maxAmmo;
        isReloading = false;

        UpdateHUDAmmo();

        Debug.Log("Reloaded!");
    }

    private void UpdateHUDAmmo()
    {
        if (HUDManager.Instance != null && weaponData != null)
        {
            HUDManager.Instance.UpdateAmmoUI(currentAmmo, weaponData.maxAmmo);
        }
    }
}