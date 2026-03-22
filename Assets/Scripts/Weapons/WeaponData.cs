using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Weapons/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("Stats")]
    public float damage = 10f;
    public float bulletSpeed = 40f;
    public float fireRate = 0.1f;
    public int maxAmmo = 30;
    public float reloadTime = 1.5f;

    [Header("Audio")]
    public AudioClip shootSound;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Effects")]
    public GameObject muzzleFlashPrefab;
}