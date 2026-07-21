using System.Diagnostics;
using System.Security.Principal;

namespace WindowsStorageCleaner.Services;

public class AdminService : IAdminService
{
    public bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public bool RestartAsAdmin()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var startInfo = new ProcessStartInfo
            {
                FileName = process.MainModule?.FileName ?? "WindowsStorageCleaner.exe",
                UseShellExecute = true,
                Verb = "runas",
                Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1))
            };
            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
