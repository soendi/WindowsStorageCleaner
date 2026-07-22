using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

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

    public static string ProfileDisplay(string p) => p switch
    {
        "auto" => "Auto (automatisch)",
        _ => p
    };

    public bool IsTaskInstalled()
    {
        return RunSchtasks($"/query /tn \"{TaskName}\" /fo LIST").exitCode == 0;
    }

    public TaskInfo? GetTaskDetails()
    {
        var (exitCode, output) = RunSchtasks($"/query /tn \"{TaskName}\" /fo LIST /v");
        if (exitCode != 0) return null;

        var info = new TaskInfo();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var colonIdx = line.IndexOf(':');
            if (colonIdx < 0) continue;
            var key = line.Substring(0, colonIdx).Trim().ToLowerInvariant();
            var val = line.Substring(colonIdx + 1).Trim();

            if (key.Contains("nchste") && key.Contains("laufzeit"))
                info.NextRun = val;
            else if (key == "status")
                info.Status = val;
            else if (key.Contains("auszuf") && (key.Contains("aufgabe") || key.Contains("aufg")))
                info.CommandLine = val;
            else if (key.Contains("zeitplan") || key.Contains("zeitplantyp"))
                info.ScheduleType = val;
            else if (key == "tage")
                info.ScheduleDays = val;
            else if (key == "monate")
                info.ScheduleMonths = val;
            else if (key.Contains("startzeit"))
                info.ScheduleTime = val;
        }

        var match = Regex.Match(info.CommandLine, @"--profile (\w+)");
        if (match.Success)
            info.Profile = match.Groups[1].Value;

        // Build friendly name
        var freq = info.ScheduleType;
        if (info.ScheduleDays != "Nicht zutreffend" && !string.IsNullOrEmpty(info.ScheduleDays))
            freq += $" ({info.ScheduleDays})";
        if (info.ScheduleMonths != "Nicht zutreffend" && !string.IsNullOrEmpty(info.ScheduleMonths))
            freq += $" – {info.ScheduleMonths}";

        info.FriendlyName = $"{freq} – Profil: {ProfileDisplay(info.Profile)}";
        return info;
    }

    public bool IsTaskEnabled()
    {
        var info = GetTaskDetails();
        return info?.Status?.Equals("Bereit", StringComparison.OrdinalIgnoreCase) == true;
    }

    public void SetTaskEnabled(bool enabled)
    {
        RunSchtasks($"/change /tn \"{TaskName}\" /{(enabled ? "ENABLE" : "DISABLE")}");
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
    }

    public void RemoveTask()
    {
        RunSchtasks($"/delete /tn \"{TaskName}\" /f");
    }

    private static (int exitCode, string output) RunSchtasks(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage),
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return (-1, "");
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return (p.ExitCode, output);
        }
        catch { return (-1, ""); }
    }

    public class TaskInfo
    {
        public string NextRun { get; set; } = "";
        public string ScheduleTime { get; set; } = "";
        public string ScheduleType { get; set; } = "";
        public string ScheduleDays { get; set; } = "";
        public string ScheduleMonths { get; set; } = "";
        public string Profile { get; set; } = "";
        public string CommandLine { get; set; } = "";
        public string Status { get; set; } = "";
        public string FriendlyName { get; set; } = "";
        public bool IsEnabled => Status?.Equals("Bereit", StringComparison.OrdinalIgnoreCase) == true;
        public string ToggleLabel => IsEnabled ? "Deaktivieren" : "Aktivieren";
    }
}
