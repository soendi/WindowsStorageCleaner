using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WindowsStorageCleaner.Views;

public partial class ConfirmDialog : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public bool IsDarkTheme { get; set; }

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var useDark = IsDarkTheme ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
    }
}
