using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WindowsStorageCleaner.Views;

public partial class ShortcutWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public bool IsDarkTheme { get; set; }

    private static readonly (string Label, string Args)[] Profiles =
    {
        ("Programm normal starten", ""),
        ("Auto (automatische Auswahl)", "--profile auto"),
        ("Benutzerdefiniert", "--profile custom"),
        ("Sicher", "--profile sicher"),
        ("Standard", "--profile standard"),
        ("Gründlich", "--profile gründlich"),
        ("Maximal", "--profile maximal"),
        ("Alles", "--profile alles"),
    };

    public ShortcutWindow()
    {
        InitializeComponent();
        ProfileCombo.ItemsSource = Profiles.Select(p => p.Label);
        ProfileCombo.SelectedIndex = 0;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var useDark = IsDarkTheme ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
    }

    private void OnCreateShortcut(object sender, RoutedEventArgs e)
    {
        var idx = ProfileCombo.SelectedIndex;
        if (idx < 0) return;

        var (label, args) = Profiles[idx];
        var exePath = App.GetExePath();
        var shortcutName = label == "Programm normal starten"
            ? "Windows Storage Cleaner"
            : $"Windows Storage Cleaner ({label.Split('(')[0].Trim()})";

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var shortcutPath = Path.Combine(desktop, $"{shortcutName}.lnk");

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) { MessageBox.Show("Konnte keinen Shell-Typ erstellen.", "Fehler"); return; }
            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = exePath;
            shortcut.Arguments = args;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
            shortcut.Description = $"Windows Storage Cleaner – {label}";
            shortcut.Save();
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);

            MessageBox.Show($"Verknüpfung erstellt:\n{shortcutPath}", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Erstellen der Verknüpfung:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
