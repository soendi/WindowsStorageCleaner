using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace WindowsStorageCleaner.Models;

public class CleanupItem : INotifyPropertyChanged
{
    private bool _isChecked;
    private bool _isIndeterminate;
    private bool _isInfoVisible;
    private long _estimatedSize;
    private bool _isRunning;
    private string _statusText = string.Empty;
    private long _actualFreed;
    private CleanupState _state = CleanupState.Pending;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InfoText { get; set; } = string.Empty;

    public bool IsInfoVisible
    {
        get => _isInfoVisible;
        set { _isInfoVisible = value; OnPropertyChanged(nameof(IsInfoVisible)); }
    }

    public string Icon { get; set; } = "mdi:delete";
    public bool IsIrreversible { get; set; }
    public string IrreversibleWarning { get; set; } = string.Empty;
    public CleanupCategory Category { get; set; }
    public CleanupAction Action { get; set; } = CleanupAction.None;
    public string ActionData { get; set; } = string.Empty;
    public ObservableCollection<CleanupItem> Children { get; set; } = new();
    public bool HasChildren => Children.Count > 0;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                _isChecked = value;
                OnPropertyChanged(nameof(IsChecked));
                if (HasChildren)
                {
                    foreach (var child in Children)
                        child.IsChecked = value;
                }
                UpdateParentIndeterminate();
            }
        }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set { _isIndeterminate = value; OnPropertyChanged(nameof(IsIndeterminate)); }
    }

    public long EstimatedSize
    {
        get => _estimatedSize;
        set { _estimatedSize = value; OnPropertyChanged(nameof(EstimatedSize)); OnPropertyChanged(nameof(EstimatedSizeText)); }
    }

    public string EstimatedSizeText => EstimatedSize >= 0
        ? FormatSize(EstimatedSize)
        : "Wird berechnet...";

    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(nameof(IsRunning)); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
    }

    public long ActualFreed
    {
        get => _actualFreed;
        set { _actualFreed = value; OnPropertyChanged(nameof(ActualFreed)); OnPropertyChanged(nameof(ActualFreedText)); }
    }

    public string ActualFreedText => FormatSize(ActualFreed);

    public CleanupState State
    {
        get => _state;
        set { _state = value; OnPropertyChanged(nameof(State)); OnPropertyChanged(nameof(StateIcon)); OnPropertyChanged(nameof(StateColor)); }
    }

    public string StateIcon => State switch
    {
        CleanupState.Pending => "⏳",
        CleanupState.Analyzing => "🔍",
        CleanupState.Running => "⚙️",
        CleanupState.Completed => "✅",
        CleanupState.Skipped => "⏭️",
        CleanupState.Failed => "❌",
        CleanupState.Warning => "⚠️",
        _ => ""
    };

    public SolidColorBrush StateColor => State switch
    {
        CleanupState.Completed => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
        CleanupState.Failed => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
        CleanupState.Running => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
        CleanupState.Warning => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
        _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
    };

    public CleanupItem? Parent { get; set; }

    private void UpdateParentIndeterminate()
    {
        if (Parent == null || !Parent.HasChildren) return;
        int checkedCount = Parent.Children.Count(c => c.IsChecked);
        if (checkedCount == 0)
            Parent.IsChecked = false;
        else if (checkedCount == Parent.Children.Count)
            Parent.IsChecked = true;
        else
            Parent.IsIndeterminate = true;
    }

    public long GetTotalEstimatedSize()
    {
        if (!IsChecked) return 0;
        if (HasChildren)
            return Children.Where(c => c.IsChecked).Sum(c => c.GetTotalEstimatedSize());
        return EstimatedSize;
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 0) return "Unbekannt";
        string[] suffixes = { "Byte", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return order == 0
            ? $"{size} {suffixes[order]}"
            : $"{size:F2} {suffixes[order]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum CleanupCategory
{
    DiskCleanup,
    ComponentCleanup,
    TempFiles,
    UpdateCache,
    Hibernate,
    BrowserCache,
    SystemLogs,
    Additional
}

public enum CleanupAction
{
    None,
    CleanMgr,
    DISM,
    DeleteDirectory,
    DeleteFiles,
    StopServices,
    RunCommand,
    RunPowerShell,
    ClearEventLog,
    CompactOs
}

public enum CleanupState
{
    Pending,
    Analyzing,
    Running,
    Completed,
    Skipped,
    Failed,
    Warning
}
