using UnityEngine;

public class Crosshair : MonoBehaviour
{
    public Color crosshairColor = Color.white;
    public int crosshairSize = 10;
    public int crosshairThickness = 2;
    public int crosshairGap = 4;

    private Texture2D crosshairTexture;

    void Start()
    {
        crosshairTexture = new Texture2D(1, 1);
        crosshairTexture.SetPixel(0, 0, crosshairColor);
        crosshairTexture.Apply();
    }

    void OnGUI()
    {
        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;

        // Left
        GUI.DrawTexture(new Rect(centerX - crosshairGap - crosshairSize, centerY - crosshairThickness / 2f, crosshairSize, crosshairThickness), crosshairTexture);
        // Right
        GUI.DrawTexture(new Rect(centerX + crosshairGap, centerY - crosshairThickness / 2f, crosshairSize, crosshairThickness), crosshairTexture);
        // Up
        GUI.DrawTexture(new Rect(centerX - crosshairThickness / 2f, centerY - crosshairGap - crosshairSize, crosshairThickness, crosshairSize), crosshairTexture);
        // Down
        GUI.DrawTexture(new Rect(centerX - crosshairThickness / 2f, centerY + crosshairGap, crosshairThickness, crosshairSize), crosshairTexture);
    }
}
