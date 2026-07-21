namespace WindowsStorageCleaner.Services;

public interface ISettingsService
{
    T GetValue<T>(string key, T defaultValue);
    void SetValue<T>(string key, T value);
    void Save();
    void Load();
}
