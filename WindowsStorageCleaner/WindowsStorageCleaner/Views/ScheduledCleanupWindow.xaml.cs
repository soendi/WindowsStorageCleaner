using System.Collections.ObjectModel;
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
    private readonly ObservableCollection<ScheduledCleanupService.TaskInfo> _tasks = new();

    public ScheduledCleanupWindow()
    {
        InitializeComponent();
        FrequencyCombo.ItemsSource = ScheduledCleanupService.Frequencies.Select(f => f.Label);
        FrequencyCombo.SelectedIndex = 0;
        ProfileCombo.ItemsSource = ScheduledCleanupService.ProfileOptions;
        ProfileCombo.SelectedIndex = 2;
        ScheduleList.ItemsSource = _tasks;
        RefreshList();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var useDark = IsDarkTheme ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
    }

    private void RefreshList()
    {
        _tasks.Clear();
        var info = _service.GetTaskDetails();
        if (info != null)
            _tasks.Add(info);
    }

    private void OnAddScheduleClick(object sender, RoutedEventArgs e)
    {
        var freqIdx = FrequencyCombo.SelectedIndex;
        var profileIdx = ProfileCombo.SelectedIndex;
        if (freqIdx < 0 || profileIdx < 0) return;

        var profileName = ScheduledCleanupService.ProfileOptions[profileIdx];
        _service.InstallTask(freqIdx, profileName);
        RefreshList();
    }

    private void OnToggleTask(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ScheduledCleanupService.TaskInfo info)
            _service.SetTaskEnabled(!info.IsEnabled);
        RefreshList();
    }

    private void OnDeleteTask(object sender, RoutedEventArgs e)
    {
        _service.RemoveTask();
        RefreshList();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnComboBoxDropDownOpened(object sender, EventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox comboBox
            && comboBox.Template.FindName("PART_Popup", comboBox) is System.Windows.Controls.Primitives.Popup popup
            && popup.Child is System.Windows.Controls.Border border)
        {
            border.MinWidth = comboBox.ActualWidth;
        }
    }
}
