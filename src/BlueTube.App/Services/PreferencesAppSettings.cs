using BlueTube.Core.Services;

namespace BlueTube.App.Services;

/// <summary>Non-secret settings in MAUI Preferences.</summary>
public sealed class PreferencesAppSettings : IAppSettings
{
    public int MinScore
    {
        get => Preferences.Default.Get("min_score", 40);
        set => Preferences.Default.Set("min_score", Math.Clamp(value, 0, 100));
    }

    public string LlmModel
    {
        get => Preferences.Default.Get("llm_model", LlmQualityFilter.DefaultModel);
        set => Preferences.Default.Set("llm_model", value);
    }
}
