using System.Windows;

namespace WindowsStorageCleaner.Views;

public partial class UpdateProgressWindow : Window
{
    public UpdateProgressWindow()
    {
        InitializeComponent();
    }

    public void SetProgress(double fraction)
    {
        ProgressBar.Value = fraction * 100;
        PercentText.Text = $"{(int)(fraction * 100)}%";
    }
}
