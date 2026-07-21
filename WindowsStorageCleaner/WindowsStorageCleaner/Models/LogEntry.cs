using System.ComponentModel;

namespace WindowsStorageCleaner.Models;

public class LogEntry : INotifyPropertyChanged
{
    private string _timestamp = string.Empty;
    private string _message = string.Empty;
    private LogLevel _level = LogLevel.Info;
    private bool _indent;

    public string Timestamp
    {
        get => _timestamp;
        set { _timestamp = value; OnPropertyChanged(nameof(Timestamp)); }
    }
    public string Message
    {
        get => _message;
        set { _message = value; OnPropertyChanged(nameof(Message)); }
    }
    public bool Indent
    {
        get => _indent;
        set { _indent = value; OnPropertyChanged(nameof(Indent)); }
    }
    public LogLevel Level
    {
        get => _level;
        set { _level = value; OnPropertyChanged(nameof(Level)); OnPropertyChanged(nameof(LevelPrefix)); }
    }
    public string LevelPrefix => Level switch
    {
        LogLevel.Info => " INFO",
        LogLevel.Warning => " WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Success => "   OK",
        _ => " INFO"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Success
}
