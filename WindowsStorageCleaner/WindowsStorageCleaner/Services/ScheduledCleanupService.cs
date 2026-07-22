using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace WindowsStorageCleaner.Services;

public class ScheduledCleanupService
{
    private const string TaskName = "WindowsStorageCleaner";

    public static readonly (string Label, string SchtaskSc, string? SchtaskMo)[] Frequencies =
    {
        ("jede Woche", "WEEKLY", null),
        ("jede 2. Woche", "WEEKLY", "2"),
        ("jeden Monat", "MONTHLY", null),
        ("jeden 2. Monat", "MONTHLY", "2"),
        ("jeden 3. Monat", "MONTHLY", "3"),
    };

    public static readonly string[] ProfileOptions =
    {
        "auto", "sicher", "standard", "gründlich", "maximal", "alles"
    };

    public static string ProfileDisplay(string p) => p == "auto" ? "Auto (automatisch)" : p;

    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "WindowsStorageCleaner", "schedule.json");

    public bool IsTaskInstalled()
    {
        return RunSchtasks($"/query /tn \"{TaskName}\"").exitCode == 0;
    }

    public TaskInfo? GetTaskDetails()
    {
        if (!IsTaskInstalled()) return null;

        var fromFile = LoadScheduleSettings();
        if (fromFile != null) return fromFile;

        // Fallback: parse CSV output to recover existing task info
        var (_, csv) = RunSchtasks($"/query /tn \"{TaskName}\" /fo CSV /v");
        var fallback = ParseCsvTaskInfo(csv);
        if (fallback != null)
        {
            SaveScheduleSettings(fallback);
            return fallback;
        }

        return null;
    }

    private static TaskInfo? ParseCsvTaskInfo(string csv)
    {
        try
        {
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return null;

            // CSV format: "Host","Task","Status","Next Run","Schedule Type","Start Time","Days",...
            var fields = ParseCsvLine(lines[1]);
            if (fields.Count < 7) return null;

            var status = fields[2].Trim('"');
            var nextRun = fields[3].Trim('"');
            var scheduleType = fields[4].Trim('"');
            var days = fields[6].Trim('"');

            var friendly = $"{scheduleType} ({days}) – Profil: unbekannt";
            return new TaskInfo
            {
                NextRun = nextRun,
                Frequency = $"{scheduleType} ({days})",
                Profile = "unbekannt",
                Status = status,
                FriendlyName = friendly
            };
        }
        catch { return null; }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (char c in line)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }

    public void InstallTask(int frequencyIndex, string profileName)
    {
        RemoveTask();
        var freq = Frequencies[frequencyIndex];
        var sc = freq.SchtaskSc;
        var args = $"/create /tn \"{TaskName}\" /tr \"'{App.GetExePath()}' --profile {profileName}\" /sc {sc} /st 10:00 /f";

        if (freq.SchtaskMo != null)
            args += sc == "WEEKLY" ? $" /d MON /mo {freq.SchtaskMo}" : $" /d 1 /mo {freq.SchtaskMo}";
        else if (sc == "WEEKLY")
            args += " /d MON";
        else if (sc == "MONTHLY")
            args += " /d 1";

        args += " /ru SYSTEM /rl HIGHEST";
        RunSchtasks(args);

        var info = new TaskInfo
        {
            Frequency = freq.Label,
            Profile = profileName,
            Status = "Bereit",
            NextRun = "wird vom Task Scheduler verwaltet",
            FriendlyName = $"{freq.Label} – Profil: {ProfileDisplay(profileName)}"
        };
        SaveScheduleSettings(info);
    }

    public void RemoveTask()
    {
        RunSchtasks($"/delete /tn \"{TaskName}\" /f");
        if (File.Exists(SettingsPath))
            File.Delete(SettingsPath);
    }

    public void SetTaskEnabled(bool enabled)
    {
        RunSchtasks($"/change /tn \"{TaskName}\" /{(enabled ? "ENABLE" : "DISABLE")}");
        var info = LoadScheduleSettings();
        if (info != null)
        {
            info.Status = enabled ? "Bereit" : "Deaktiviert";
            SaveScheduleSettings(info);
        }
    }

    private TaskInfo? LoadScheduleSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<TaskInfo>(json);
        }
        catch { return null; }
    }

    private void SaveScheduleSettings(TaskInfo info)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    private static (int exitCode, string output) RunSchtasks(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p == null) return (-1, "");
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return (p.ExitCode, stdout);
        }
        catch { return (-1, ""); }
    }

    public class TaskInfo
    {
        public string NextRun { get; set; } = "";
        public string Frequency { get; set; } = "";
        public string Profile { get; set; } = "";
        public string Status { get; set; } = "";
        public string FriendlyName { get; set; } = "";
        public bool IsEnabled => Status?.Equals("Bereit", StringComparison.OrdinalIgnoreCase) == true;
        public string ToggleLabel => IsEnabled ? "Deaktivieren" : "Aktivieren";
    }
}
