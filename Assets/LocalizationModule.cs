using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string localizationKey;

    private TMP_Text _text;

    private void Start()
    {
        _text = GetComponent<TMP_Text>();

        if (LocalizationManager.Instance == null)
        {
            Debug.LogError(
                $"LocalizationManager not found before LocalizedText: {name}",
                this);
            return;
        }

        LocalizationManager.Instance.OnLanguageChanged += UpdateText;
        UpdateText();
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= UpdateText;
    }

    private void UpdateText()
    {
        _text.text = LocalizationManager.Instance.Get(localizationKey);
    }
}
