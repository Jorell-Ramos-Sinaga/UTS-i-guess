using System;
using System.Collections.Generic;
using UnityEngine;

public enum Language
{
    English = 0,
    Indonesian = 1
}

[DefaultExecutionOrder(-100)]
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    public event Action OnLanguageChanged;
    public Language CurrentLanguage { get; private set; }

    private const string LanguageKey = "Language";

    private readonly Dictionary<string, string[]> texts = new Dictionary<string, string[]>
    {
        { "title",      new[] { "CRYSTALFORGE", "CRYSTALFORGE" } },
        { "play",       new[] { "Play", "Main" } },
        { "options",    new[] { "Options", "Pengaturan" } },
        { "quit",       new[] { "Quit", "Keluar" } },
        { "music",      new[] { "Music", "Musik" } },
        { "sfx",        new[] { "Sound Effects", "Efek Suara" } },
        { "fullscreen", new[] { "Fullscreen", "Layar Penuh" } },
        { "language",   new[] { "Language", "Bahasa" } },
        { "back",       new[] { "Back", "Kembali" } }
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentLanguage = (Language)PlayerPrefs.GetInt(LanguageKey, (int)Language.English);
    }

    public string Get(string key)
    {
        if (texts.TryGetValue(key, out string[] values))
        {
            return values[(int)CurrentLanguage];
        }

        Debug.LogWarning("Localization key not found: " + key);
        return key;
    }

    public void SetLanguage(Language language)
    {
        CurrentLanguage = language;
        PlayerPrefs.SetInt(LanguageKey, (int)language);
        PlayerPrefs.Save();

        OnLanguageChanged?.Invoke();
    }
}