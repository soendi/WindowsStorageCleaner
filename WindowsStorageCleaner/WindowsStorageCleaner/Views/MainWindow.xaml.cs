using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using WindowsStorageCleaner.Models;
using WindowsStorageCleaner.ViewModels;

namespace WindowsStorageCleaner.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _popupMouseTimer = new();
    private Button? _activeInfoButton;
    private bool _isMouseOverPopup;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        Loaded += async (s, e) =>
        {
            await _viewModel.InitializeAsync();
            ApplyDwmTheme(_viewModel.IsDarkTheme);
        };
        _popupMouseTimer.Interval = TimeSpan.FromMilliseconds(200);
        _popupMouseTimer.Tick += OnPopupMouseTimerTick;
        UpdateStartButtonText();
    }

    private void ApplyDwmTheme(bool dark)
    {
        try
        {
            if (Environment.OSVersion.Version.Major < 10) return;
            var hwnd = new WindowInteropHelper(this).Handle;
            int useDark = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }
        catch { }
    }

    private void UpdateStartButtonText()
    {
        StartButton.Content = _viewModel.IsRunning ? "Wird ausgef\u00fchrt..." : "Bereinigung starten";
    }

    private async void OnAnalyzeClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.AnalyzeAsync();
    }

    private async void OnStartCleanupClick(object sender, RoutedEventArgs e)
    {
        StartButton.Content = "Wird ausgef\u00fchrt...";
        await _viewModel.ExecuteCleanupAsync();
        StartButton.Content = "Bereinigung starten";
    }

    private async void OnUpdateCheckClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.CheckForUpdateAsync();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _viewModel.CancelCleanup();
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ExportLog();
    }

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleTheme();
        ApplyDwmTheme(_viewModel.IsDarkTheme);
        RefreshComboBoxColors();
    }

    private void RefreshComboBoxColors()
    {
        MatchPopupWidth(ProfileComboBox);
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow
        {
            Owner = this,
            IsDarkTheme = _viewModel.IsDarkTheme
        };
        about.ShowDialog();
    }

    private void OnCleanupInfoClick(object sender, RoutedEventArgs e)
    {
        var entries = new List<CleanupInfoEntry>();
        CollectInfoEntries(_viewModel.CleanupItems, entries);
        var window = new CleanupInfoWindow
        {
            Owner = this,
            IsDarkTheme = _viewModel.IsDarkTheme,
            DataContext = entries
        };
        window.ShowDialog();
    }

    private static void CollectInfoEntries(ObservableCollection<CleanupItem> items, List<CleanupInfoEntry> entries)
    {
        foreach (var item in items)
        {
            if (item.HasChildren)
            {
                CollectInfoEntries(item.Children, entries);
            }
            else if (!string.IsNullOrEmpty(item.InfoText))
            {
                entries.Add(new CleanupInfoEntry { Name = item.Name, InfoText = item.InfoText });
            }
        }
    }

    private void OnCloseResultClick(object sender, RoutedEventArgs e)
    {
        _viewModel.IsResultVisible = false;
    }

    private void OnComboBoxDropDownOpened(object? sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox) return;
        MatchPopupWidth(comboBox);
    }

    private static void MatchPopupWidth(ComboBox comboBox)
    {
        if (comboBox.Template.FindName("PART_Popup", comboBox) is Popup popup
            && popup.Child is Border border)
        {
            border.MinWidth = comboBox.ActualWidth;
        }
    }

    private void OnInfoButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not CleanupItem item || string.IsNullOrEmpty(item.InfoText))
            return;
        InfoPopupText.Text = item.InfoText;
        InfoPopup.PlacementTarget = button;
        InfoPopup.HorizontalOffset = 6;
        _isMouseOverPopup = false;
        InfoPopup.IsOpen = true;
        _activeInfoButton = button;
        _popupMouseTimer.Start();
    }

    private void OnInfoPopupClosed(object? sender, EventArgs e)
    {
        _popupMouseTimer.Stop();
        _activeInfoButton = null;
    }

    private void OnInfoPopupMouseEnter(object sender, MouseEventArgs e)
    {
        _isMouseOverPopup = true;
    }

    private void OnInfoPopupMouseLeave(object sender, MouseEventArgs e)
    {
        _isMouseOverPopup = false;
    }

    private void OnPopupMouseTimerTick(object? sender, EventArgs e)
    {
        if (!InfoPopup.IsOpen || _activeInfoButton == null || _isMouseOverPopup)
            return;
        var mousePos = Mouse.GetPosition(this);
        var popupChild = InfoPopup.Child as FrameworkElement;
        if (popupChild == null) return;
        var popupPos = popupChild.TranslatePoint(new Point(0, 0), this);
        var popupRect = new Rect(popupPos.X, popupPos.Y, popupChild.ActualWidth, popupChild.ActualHeight);
        var expandedRect = new Rect(popupRect.X - 200, popupRect.Y - 200,
                                     popupRect.Width + 400, popupRect.Height + 400);
        if (!expandedRect.Contains(mousePos))
            InfoPopup.IsOpen = false;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        _viewModel.SaveSettings();
    }
}
