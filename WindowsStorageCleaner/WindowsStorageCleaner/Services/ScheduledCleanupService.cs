using System.Diagnostics;

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
        "auto (automatisch)", "sicher", "standard", "gründlich", "maximal", "alles"
    };

    public bool IsTaskInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks", $"/query /tn \"{TaskName}\" /fo LIST")
            {
                UseShellExecute = false, RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    public void InstallTask(int frequencyIndex, string profileName)
    {
        var freq = Frequencies[frequencyIndex];

        var sc = freq.SchtaskSc;
        var args = $"/create /tn \"{TaskName}\" /tr \"'{App.GetExePath()}' --profile {profileName}\" /sc {sc} /st 10:00 /f";

        if (freq.SchtaskMo != null)
        {
            if (sc == "WEEKLY")
                args += $" /d MON /mo {freq.SchtaskMo}";
            else
                args += $" /d 1 /mo {freq.SchtaskMo}";
        }
        else if (sc == "WEEKLY")
            args += " /d MON";
        else if (sc == "MONTHLY")
            args += " /d 1";

        args += " /ru SYSTEM /rl HIGHEST";

        var psi = new ProcessStartInfo("schtasks", args)
        {
            UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p == null) return;
        p.WaitForExit(10000);
    }

    public void RemoveTask()
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks", $"/delete /tn \"{TaskName}\" /f")
            {
                UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return;
            p.WaitForExit(5000);
        }
        catch { }
    }
}
