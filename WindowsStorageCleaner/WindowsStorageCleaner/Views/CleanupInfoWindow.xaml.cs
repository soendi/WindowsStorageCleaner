using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace WindowsStorageCleaner.Views;

public partial class CleanupInfoWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public bool IsDarkTheme { get; set; }

    public CleanupInfoWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var useDark = IsDarkTheme ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
