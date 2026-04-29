using UnityEngine;
using UnityEngine.Audio;

public class ApplySavedAudioSettings : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;

    private void Start()
    {
        float savedMaster = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float savedMusic = PlayerPrefs.GetFloat("MusicVolume", 1f);

        audioMixer.SetFloat("MasterVolume", Mathf.Log10(Mathf.Max(savedMaster, 0.0001f)) * 20f);
        audioMixer.SetFloat("MusicVolume", Mathf.Log10(Mathf.Max(savedMusic, 0.0001f)) * 20f);
    }
}