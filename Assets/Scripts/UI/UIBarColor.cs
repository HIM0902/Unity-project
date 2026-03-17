using UnityEngine;
using UnityEngine.UI;

public class UIBarColor : MonoBehaviour
{
    // Changes the bar fill color based on the current health percent
    public Slider Slider;   // HealthBar Slider here
    public Image fillImage;       // Fill here

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Slider == null || fillImage == null) return;

        // Convert current value into a percent (0 to 1)
        float percent = Slider.value / Slider.maxValue;

        // 70%+ = green, 30% - 69% = yellow, under 30% = red
        if (percent >= 0.70f)
            fillImage.color = Color.green;
        else if (percent >= 0.30f)
            fillImage.color = Color.yellow;
        else
            fillImage.color = Color.red;
    }
}