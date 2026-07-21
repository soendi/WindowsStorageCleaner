namespace WindowsStorageCleaner.Models;

public class CleanupResult
{
    public long TotalEstimated { get; set; }
    public long TotalFreed { get; set; }
    public long FreeSpaceBefore { get; set; }
    public long FreeSpaceAfter => FreeSpaceBefore + TotalFreed;
    public List<CleanupItemResult> ItemResults { get; set; } = new();
    public string TotalFreedText => CleanupItem.FormatSize(TotalFreed);
    public string FreeSpaceBeforeText => CleanupItem.FormatSize(FreeSpaceBefore);
    public string FreeSpaceAfterText => CleanupItem.FormatSize(FreeSpaceAfter);
}

public class CleanupItemResult
{
    public string ItemName { get; set; } = string.Empty;
    public long ExpectedSize { get; set; }
    public long ActualFreed { get; set; }
    public CleanupState State { get; set; }
    public string ExpectedText => CleanupItem.FormatSize(ExpectedSize);
    public string ActualText => CleanupItem.FormatSize(ActualFreed);
}
