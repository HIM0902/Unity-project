using UnityEngine;
using UnityEngine.Events;

public class PlayerAmmo : MonoBehaviour
{
    [Header("Ammo Settings")]
    public int magazineSize = 30;
    public int currentAmmo;
    public int reserveAmmo = 120;

    [Header("Events")]
    public UnityEvent onAmmoChanged;
    public UnityEvent onOutOfAmmo;

    public static PlayerAmmo Instance { get; private set; }

    private bool isReloading = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        currentAmmo = magazineSize;
    }

    public bool UseAmmo(int amount = 1)
    {
        if (currentAmmo <= 0)
        {
            onOutOfAmmo?.Invoke();
            return false;
        }

        currentAmmo = Mathf.Max(currentAmmo - amount, 0);
        onAmmoChanged?.Invoke();
        return true;
    }

    public void Reload()
    {
        if (isReloading) return;
        if (reserveAmmo <= 0) return;
        if (currentAmmo == magazineSize) return;

        int needed = magazineSize - currentAmmo;
        int toReload = Mathf.Min(needed, reserveAmmo);

        currentAmmo += toReload;
        reserveAmmo -= toReload;

        onAmmoChanged?.Invoke();
    }

    public void AddReserveAmmo(int amount)
    {
        reserveAmmo += amount;
        onAmmoChanged?.Invoke();
    }

    public bool IsEmpty() => currentAmmo <= 0;
    public bool IsOutOfAmmo() => currentAmmo <= 0 && reserveAmmo <= 0;
}