using WindowsStorageCleaner.Models;

namespace WindowsStorageCleaner.Services;

public interface ICleanupService
{
    Task<long> AnalyzeItemAsync(CleanupItem item, IProgress<LogEntry> progress);
    Task<long> ExecuteItemAsync(CleanupItem item, IProgress<LogEntry> progress, CancellationToken token);
    Task<SystemInfo> GetSystemInfoAsync();
}
