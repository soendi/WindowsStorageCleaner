using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WindowsStorageCleaner.Services;

namespace WindowsStorageCleaner.Views;

public partial class ScheduledCleanupWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public bool IsDarkTheme { get; set; }
    private readonly ScheduledCleanupService _service = new();

    public ScheduledCleanupWindow()
    {
        InitializeComponent();
        FrequencyCombo.ItemsSource = ScheduledCleanupService.Frequencies.Select(f => f.Label);
        ProfileCombo.ItemsSource = ScheduledCleanupService.ProfileOptions;
        UpdateStatus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var useDark = IsDarkTheme ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
    }

    private void UpdateStatus()
    {
        if (_service.IsTaskInstalled())
        {
            InstallBtn.IsEnabled = false;
            RemoveBtn.IsEnabled = true;
            StatusText.Text = "✔ Automatische Bereinigung ist eingerichtet.";
        }
        else
        {
            InstallBtn.IsEnabled = true;
            RemoveBtn.IsEnabled = false;
            StatusText.Text = "✘ Keine automatische Bereinigung eingerichtet.";
        }
    }

    private void OnInstallClick(object sender, RoutedEventArgs e)
    {
        var freqIdx = FrequencyCombo.SelectedIndex;
        var profileIdx = ProfileCombo.SelectedIndex;
        if (freqIdx < 0 || profileIdx < 0) return;

        var profileName = ScheduledCleanupService.ProfileOptions[profileIdx].Split(' ')[0];
        _service.RemoveTask();
        _service.InstallTask(freqIdx, profileName);
        UpdateStatus();
        StatusText.Text = "✔ Automatische Bereinigung wurde eingerichtet.";
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        _service.RemoveTask();
        UpdateStatus();
        StatusText.Text = "✘ Automatische Bereinigung wurde deaktiviert.";
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
