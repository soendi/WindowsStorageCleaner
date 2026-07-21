using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;

namespace WindowsStorageCleaner;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!IsRunningAsAdmin())
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var startInfo = new ProcessStartInfo
                {
                    FileName = process.MainModule?.FileName ?? "WindowsStorageCleaner.exe",
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Environment.CurrentDirectory
                };
                Process.Start(startInfo);
                Current.Shutdown();
                return;
            }
            catch
            {
                MessageBox.Show(
                    "Administratorrechte sind erforderlich.\n\n" +
                    "Bitte starten Sie die Anwendung als Administrator.",
                    "Windows Storage Cleaner",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Current.Shutdown();
                return;
            }
        }

        base.OnStartup(e);
    }

    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
