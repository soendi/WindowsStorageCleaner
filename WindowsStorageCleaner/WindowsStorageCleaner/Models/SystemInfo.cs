namespace WindowsStorageCleaner.Models;

public class SystemInfo
{
    public string OsName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string BuildNumber { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string DriveLetter { get; set; } = "C:";
    public long TotalSize { get; set; }
    public long FreeSpace { get; set; }
    public long UsedSpace => TotalSize - FreeSpace;
    public double FreePercent => TotalSize > 0 ? Math.Round((double)FreeSpace / TotalSize * 100, 1) : -1;
    public bool IsAdmin { get; set; }
    public string TotalSizeText => FormatSize(TotalSize);
    public string FreeSpaceText => FormatSize(FreeSpace);
    public string UsedSpaceText => FormatSize(UsedSpace);
    public string FreePercentText => FreePercent >= 0 ? $"{FreePercent} %" : "-";
    public ProfileLevel RecommendedProfile => FreePercent switch
    {
        >= 40 => ProfileLevel.Safe,
        >= 25 => ProfileLevel.Standard,
        >= 10 => ProfileLevel.Thorough,
        _ => ProfileLevel.Maximum
    };
    public string RecommendationReason => FreePercent switch
    {
        < 0 => "Speicherinformationen nicht verf\u00FCgbar.",
        >= 40 => $"Ausreichend Speicher frei ({FreePercent} %).",
        >= 25 => $"Nur {FreePercent} % Speicher frei.",
        >= 10 => $"Kritisch: Nur {FreePercent} % Speicher frei.",
        _ => $"Sehr kritisch: Nur {FreePercent} % Speicher frei!"
    };

    private static string FormatSize(long bytes)
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
}
