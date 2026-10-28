using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string key;

    private TMP_Text label;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        LocalizationManager.Instance.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        label.text = LocalizationManager.Instance.Get(key);
    }
}