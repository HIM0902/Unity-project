using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class AudioSettingsMenu : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;

    private void Start()
    {
        // Load saved values, or default to full volume
        float savedMaster = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float savedMusic = PlayerPrefs.GetFloat("MusicVolume", 1f);

        masterSlider.value = savedMaster;
        musicSlider.value = savedMusic;

        SetMasterVolume(savedMaster);
        SetMusicVolume(savedMusic);

        masterSlider.onValueChanged.AddListener(SetMasterVolume);
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
    }

    public void SetMasterVolume(float sliderValue)
    {
        // Avoid log(0)
        float volume = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
        audioMixer.SetFloat("MasterVolume", volume);
        PlayerPrefs.SetFloat("MasterVolume", sliderValue);
    }

    public void SetMusicVolume(float sliderValue)
    {
        float volume = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
        audioMixer.SetFloat("MusicVolume", volume);
        PlayerPrefs.SetFloat("MusicVolume", sliderValue);
    }
}