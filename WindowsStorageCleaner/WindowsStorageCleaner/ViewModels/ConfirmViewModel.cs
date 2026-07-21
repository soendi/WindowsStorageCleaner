using System.Windows.Input;

namespace WindowsStorageCleaner.ViewModels;

public class ConfirmViewModel : BaseViewModel
{
    private string _itemName = string.Empty;
    private string _warningText = string.Empty;
    private string _inputText = string.Empty;
    private bool _isConfirmed;
    private bool _canConfirm;

    public string ItemName
    {
        get => _itemName;
        set => SetProperty(ref _itemName, value);
    }

    public string WarningText
    {
        get => _warningText;
        set => SetProperty(ref _warningText, value);
    }

    public string InputText
    {
        get => _inputText;
        set
        {
            if (SetProperty(ref _inputText, value))
                CanConfirm = value.Trim().Equals("JA", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool IsConfirmed
    {
        get => _isConfirmed;
        set => SetProperty(ref _isConfirmed, value);
    }

    public bool CanConfirm
    {
        get => _canConfirm;
        set => SetProperty(ref _canConfirm, value);
    }

    public ICommand ConfirmCommand => new RelayCommand(() =>
    {
        if (!CanConfirm) return;
        IsConfirmed = true;
        Confirmed?.Invoke(this, true);
        RequestClose?.Invoke();
    });

    public ICommand CancelCommand => new RelayCommand(() =>
    {
        IsConfirmed = false;
        Confirmed?.Invoke(this, false);
        RequestClose?.Invoke();
    });

    public event EventHandler<bool>? Confirmed;
    public event Action? RequestClose;
}
