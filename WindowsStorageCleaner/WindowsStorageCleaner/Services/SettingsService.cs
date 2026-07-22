using System.IO;
using System.Text.Json;

namespace WindowsStorageCleaner.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private Dictionary<string, object> _settings = new(StringComparer.OrdinalIgnoreCase);

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "WindowsStorageCleaner");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
        Load();
    }

    public T GetValue<T>(string key, T defaultValue)
    {
        if (_settings.TryGetValue(key, out var value))
        {
            if (value is JsonElement je)
            {
                try { return JsonSerializer.Deserialize<T>(je.GetRawText())!; }
                catch { }
            }
            else if (value is T v)
            {
                return v;
            }
        }
        return defaultValue;
    }

    public void SetValue<T>(string key, T value)
    {
        _settings[key] = value!;
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
            }
        }
        catch { _settings = new(); }
    }
}
