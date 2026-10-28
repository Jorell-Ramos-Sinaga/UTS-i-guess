using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class OptionsMenu : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Display")]
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Language")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    private const string MusicParam = "MusicVolume";
    private const string SfxParam = "SFXVolume";
    private const string FullscreenKey = "Fullscreen";

    private void Start()
    {
        LoadAudioSettings();
        LoadFullscreenSetting();
        SetupLanguageDropdown();

        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        languageDropdown.onValueChanged.AddListener(SetLanguage);
    }

    private void LoadAudioSettings()
    {
        float music = PlayerPrefs.GetFloat(MusicParam, 0.75f);
        float sfx = PlayerPrefs.GetFloat(SfxParam, 0.75f);

        musicSlider.SetValueWithoutNotify(music);
        sfxSlider.SetValueWithoutNotify(sfx);

        ApplyVolume(MusicParam, music);
        ApplyVolume(SfxParam, sfx);
    }

    public void SetMusicVolume(float value)
    {
        ApplyVolume(MusicParam, value);
        PlayerPrefs.SetFloat(MusicParam, value);
    }

    public void SetSfxVolume(float value)
    {
        ApplyVolume(SfxParam, value);
        PlayerPrefs.SetFloat(SfxParam, value);
    }

    private void ApplyVolume(string parameterName, float linearValue)
    {
        audioMixer.SetFloat(parameterName, Mathf.Log10(linearValue) * 20f);
    }

    private void LoadFullscreenSetting()
    {
        bool isFullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;

        fullscreenToggle.SetIsOnWithoutNotify(isFullscreen);
        Screen.fullScreen = isFullscreen;
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(FullscreenKey, isFullscreen ? 1 : 0);
    }
    private void SetupLanguageDropdown()
    {
        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(new List<string> { "English", "Bahasa Indonesia" });

        int current = (int)LocalizationManager.Instance.CurrentLanguage;
        languageDropdown.SetValueWithoutNotify(current);
        languageDropdown.RefreshShownValue();
    }

    public void SetLanguage(int index)
    {
        LocalizationManager.Instance.SetLanguage((Language)index);
    }

    private void OnDestroy()
    {
        PlayerPrefs.Save();
    }
}