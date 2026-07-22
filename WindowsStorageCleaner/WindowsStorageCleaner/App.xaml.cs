using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using WindowsStorageCleaner.ViewModels;
using WindowsStorageCleaner.Views;

namespace WindowsStorageCleaner;

public partial class App : Application
{
    internal static string[] StartupArgs { get; private set; } = Array.Empty<string>();
    internal static string? StartupProfile { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Use Environment.GetCommandLineArgs for reliability (survives elevation)
        var allArgs = Environment.GetCommandLineArgs();
        StartupArgs = allArgs.Length > 1 ? allArgs.Skip(1).ToArray() : Array.Empty<string>();
        StartupProfile = ParseProfileArg(StartupArgs);

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
                if (StartupArgs.Length > 0)
                    startInfo.Arguments = string.Join(" ", StartupArgs);
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

        if (StartupProfile != null)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var vm = new ViewModels.MainViewModel();
            _ = vm.InitializeAsync();
            System.Windows.Threading.Dispatcher.Run();
        }
        else
        {
            new Views.MainWindow().Show();
        }
    }

    private static string? ParseProfileArg(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--profile", StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals("-p", StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    internal static string GetExePath()
    {
        return Process.GetCurrentProcess().MainModule?.FileName ?? "WindowsStorageCleaner.exe";
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
