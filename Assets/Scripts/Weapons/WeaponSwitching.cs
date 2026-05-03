using UnityEngine;

public class WeaponSwitching : MonoBehaviour
{
    public GameObject ak;
    public GameObject knife;

    void Start()
    {
        EquipAK();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            EquipAK();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            EquipKnife();
        }
    }

    void EquipAK()
    {
        ak.SetActive(true);
        knife.SetActive(false);
    }

    void EquipKnife()
    {
        ak.SetActive(false);
        knife.SetActive(true);

        Animator knifeAnimator = knife.GetComponent<Animator>();
        if (knifeAnimator != null)
        {
            knifeAnimator.SetTrigger("Equip");
        }
    }
}