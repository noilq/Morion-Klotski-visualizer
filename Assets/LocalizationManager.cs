using System;
using System.Collections.Generic;
using UnityEngine;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance;

    public enum Language
    {
        English,
        Czech
    }

    public Language CurrentLanguage { get; private set; } = Language.English;

    public event Action OnLanguageChanged;

    private Dictionary<string, string> _localizedText;

    private const string LanguagePrefKey = "ss";

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (PlayerPrefs.HasKey(LanguagePrefKey))
        {
            CurrentLanguage = (Language)PlayerPrefs.GetInt(LanguagePrefKey);
        }

        LoadLanguage(CurrentLanguage);
    }

    public void SetLanguage(Language language)
    {
        if (CurrentLanguage == language)
            return;

        CurrentLanguage = language;

        PlayerPrefs.SetInt(LanguagePrefKey, (int)language);
        PlayerPrefs.Save();

        LoadLanguage(language);
        OnLanguageChanged?.Invoke();
    }

    public string Get(string key)
    {
        if (_localizedText != null && _localizedText.TryGetValue(key, out var value))
            return value;

        return $"#{key}";
    }

    private void LoadLanguage(Language language)
    {
        _localizedText = language switch
        {
            Language.English => new Dictionary<string, string>
            {
                { "rows", "Rows" },
                { "columns", "Columns" },
                { "generate", "Generate" },
                { "language", "Language" },
                { "enable_pins", "Enable pins"}, 
                { "create_block", "Create block" },
                { "delete_block", "Delete block" },
                { "repulsion_force", "Repulsion force" },
                { "spring_force", "Spring force" },
                { "damping", "Damping" },
                { "min_distance", "Min distance" },
                { "max_velocity", "Max velocity" },
                { "velocity_treshold", "Velocity treshold" },
                { "tip", "[LMB] To view specific node state \n[RMB] To move camera \n[Mouse wheel] To distance camera\n[Z] To open graph settings panel \n[X] To open board config panel \n[C] To open regular settings panel \n[Q] To close all panels\n[F] To emergency clear graph" },
            },

            Language.Czech => new Dictionary<string, string>
            {
                { "rows", "Řádky" },
                { "columns", "Sloupce" },
                { "generate", "Generovat" },
                { "language", "Jazyk" },
                { "enable_pins", "Povolit piny"}, 
                { "create_block", "Vytvořit blok" },
                { "delete_block", "Smazat blok" },
                { "repulsion_force", "Síla odpuzování" },
                { "spring_force", "Síla pružiny" },
                { "damping", "Tlumení" },
                { "min_distance", "Min vzdálenost" },
                { "max_velocity", "Max rychlost" },
                { "velocity_treshold", "Práh rychlosti" },
                { "tip", "[Levé tlačítko myši] Pro zobrazení stavu konkrétního uzlu \n[Pravé tlačítko myši] Pro pohyb kamerou \n[Kolečko myši] Pro oddálení kamery \n[Z] Pro otevření panelu nastavení grafu \n[X] Pro otevření panelu konfigurace desky \n[C] Pro otevření panelu běžných nastavení \n[Q] Pro zavření všech panelů\n[F] Vymazat graf v případě potřeby" },
            },

            _ => null
        };
    }
}
