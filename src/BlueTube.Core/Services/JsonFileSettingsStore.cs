using System.Text.Json;

namespace BlueTube.Core.Services;

/// <summary>
/// Desktop storage for secrets and settings: one plain JSON file.
/// Not encrypted (owner's choice). On Linux the file mode is 600 (owner read/write only).
/// </summary>
public sealed class JsonFileSettingsStore : ISecretStore, IAppSettings
{
    private const string MinScoreKey = "min_score";
    private const string LlmModelKey = "llm_model";

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, string> _values;

    public JsonFileSettingsStore(string path)
    {
        _path = path;
        _values = Load(path);
    }

    public Task<string?> GetAsync(string name, CancellationToken ct = default)
    {
        lock (_lock) return Task.FromResult(_values.GetValueOrDefault(name));
    }

    public Task SetAsync(string name, string? value, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(value)) _values.Remove(name);
            else _values[name] = value.Trim();
            Save();
        }
        return Task.CompletedTask;
    }

    public int MinScore
    {
        get
        {
            lock (_lock)
                return _values.TryGetValue(MinScoreKey, out var v) && int.TryParse(v, out var i) ? i : 40;
        }
        set
        {
            lock (_lock)
            {
                _values[MinScoreKey] = Math.Clamp(value, 0, 100).ToString();
                Save();
            }
        }
    }

    public string LlmModel
    {
        get { lock (_lock) return _values.GetValueOrDefault(LlmModelKey) ?? LlmQualityFilter.DefaultModel; }
        set
        {
            lock (_lock)
            {
                _values[LlmModelKey] = value;
                Save();
            }
        }
    }

    private static Dictionary<string, string> Load(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [];
        }
        catch (JsonException)
        {
            // Bad file: start empty. User enters keys again.
            return [];
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        // Write to a temp file, then replace. A crash cannot leave a half-written file.
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true }));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(tmp, _path, overwrite: true);
    }
}
