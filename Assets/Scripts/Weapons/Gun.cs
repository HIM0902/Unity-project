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

    // ─── NEW: Reload audio ─────────────────────────────────────────
    [Header("Reload")]
    [Tooltip("Sound played when the player starts reloading. Optional but recommended.")]
    public AudioClip reloadSound;

    [Tooltip("Volume for the reload sound.")]
    [Range(0f, 1f)] public float reloadVolume = 1f;

    [Tooltip("Key the player presses to manually reload.")]
    public KeyCode reloadKey = KeyCode.R;
    // ───────────────────────────────────────────────────────────────

    private AudioSource audioSource;
    private float nextFireTime = 0f;
    private int currentAmmo;
    private bool isReloading = false;
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

        if (weaponData == null)
        {
            Debug.LogError($"[Gun] '{gameObject.name}' has no WeaponData assigned! Disabling gun.", this);
            isConfigured = false;
            enabled = false;
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

        // FIXED: split auto-reload (when empty) from manual reload (R key)
        // and ignore manual reload if mag is already full — feels more natural.

        // Auto-reload when empty
        if (currentAmmo <= 0)
        {
            StartCoroutine(Reload());
            return;
        }

        // Manual reload — only if mag isn't already full
        if (Input.GetKeyDown(reloadKey) && currentAmmo < weaponData.maxAmmo)
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

        SoundEmitter.EmitSound(transform.position, 2.5f);

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
        if (weaponData == null)
        {
            isReloading = false;
            yield break;
        }

        isReloading = true;
        Debug.Log("Reloading...");

        if (HUDManager.Instance != null) HUDManager.Instance.ShowReloading();

        // NEW: play the reload sound at the START of the reload
        if (reloadSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(reloadSound, reloadVolume);
        }

        yield return new WaitForSeconds(weaponData.reloadTime);

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