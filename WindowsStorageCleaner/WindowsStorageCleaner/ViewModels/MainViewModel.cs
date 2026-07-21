using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using WindowsStorageCleaner.Models;
using WindowsStorageCleaner.Services;

namespace WindowsStorageCleaner.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly ICleanupService _cleanupService;
    private readonly IAdminService _adminService;
    private readonly ISettingsService _settingsService;
    private readonly UpdateService _updateService;

    public MainViewModel()
    {
        _cleanupService = new CleanupService();
        _adminService = new AdminService();
        _settingsService = new SettingsService();
        _updateService = new UpdateService();
    }

    public MainViewModel(ICleanupService cleanupService, IAdminService adminService, ISettingsService settingsService, UpdateService? updateService = null)
    {
        _cleanupService = cleanupService;
        _adminService = adminService;
        _settingsService = settingsService;
        _updateService = updateService ?? new UpdateService();
    }

    public string AppVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v != null ? $"v{v.Major}.{v.Minor}.{v.Build}.{v.Revision}" : "v1.0.0.0";
        }
    }

    public async Task InitializeAsync()
    {
        SystemInfo = await _cleanupService.GetSystemInfoAsync();
        OnPropertyChanged(nameof(SystemInfo));
        InitializeCleanupItems();
        DetectWindowsTheme();
        LoadSettings();
        ApplyProfileRecommendation();
        ApplyTheme();
        _ = CheckForUpdateSilentAsync();

        if (App.StartupProfile != null)
            await RunSilentWithProfile(App.StartupProfile);
    }

    private async Task RunSilentWithProfile(string profileName)
    {
        var profile = Profiles.FirstOrDefault(p =>
            p.Name.StartsWith(profileName, StringComparison.OrdinalIgnoreCase));
        if (profile == null) return;

        SelectedProfileIndex = Profiles.IndexOf(profile);
        if (!HaveCheckedItems()) return;

        var irreversibleItems = GetSelectedIrreversibleItems();
        if (irreversibleItems.Count > 0)
        {
            var confirmed = await RequestIrreversibleConfirmation();
            if (!confirmed) return;
        }

        await ExecuteCleanupCoreAsync();
        Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
    }

    public async Task CheckForUpdateSilentAsync()
    {
        try
        {
            var version = await _updateService.CheckForUpdate();
            if (version != null)
            {
                var result = MessageBox.Show(
                    $"Version {version} ist verfügbar.\n\nMöchten Sie das Update jetzt installieren?",
                    "Update verfügbar", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                    await DownloadAndInstallUpdate(version);
            }
        }
        catch { }
    }

    public async Task CheckForUpdateAsync()
    {
        var version = await _updateService.CheckForUpdate();
        if (version == null)
        {
            MessageBox.Show("Sie haben die aktuelle Version.", "Kein Update verfügbar", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var result = MessageBox.Show(
            $"Version {version} ist verfügbar ( aktuell: {_updateService.CurrentVersion}).\n\nMöchten Sie das Update jetzt installieren?",
            "Update verfügbar", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
            await DownloadAndInstallUpdate(version);
    }

    private async Task DownloadAndInstallUpdate(Version version)
    {
        try
        {
            _updateService.DownloadProgress += p => { };
            var ok = await _updateService.DownloadAndInstall(version);
            if (ok)
                Application.Current.Shutdown();
            else
                MessageBox.Show("Download fehlgeschlagen.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Update fehlgeschlagen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private SystemInfo _systemInfo = new();
    public SystemInfo SystemInfo
    {
        get => _systemInfo;
        set
        {
            if (SetProperty(ref _systemInfo, value))
            {
                OnPropertyChanged(nameof(RecommendationText));
            }
        }
    }

    public ObservableCollection<CleanupItem> CleanupItems { get; } = new();

    public List<CleanupProfile> Profiles { get; } = new()
    {
        new CleanupProfile
        {
            Name = "Nichts", Description = "Alle Checkboxen deaktivieren",
            Level = ProfileLevel.None,
            EnabledItemIds = new List<string>()
        },
        new CleanupProfile
        {
            Name = "Sicher (empfohlen)", Description = "Nur temporäre und sichere Dateien",
            Level = ProfileLevel.Safe,
            EnabledItemIds = new List<string> { "usertemp", "wintemp", "recyclebin", "shadercache", "thumbnails", "updatecache" }
        },
        new CleanupProfile
        {
            Name = "Standard", Description = "Zusätzlich DISM und Browser-Cache",
            Level = ProfileLevel.Standard,
            EnabledItemIds = new List<string> { "usertemp", "wintemp", "recyclebin", "shadercache", "thumbnails", "updatecache",
                "startcompcleanup", "browsercache", "errorreports", "updatelogs" }
        },
        new CleanupProfile
        {
            Name = "Gründlich", Description = "Zusätzlich ResetBase und Defender-Cache",
            Level = ProfileLevel.Thorough,
            EnabledItemIds = new List<string> { "usertemp", "wintemp", "recyclebin", "shadercache", "thumbnails", "updatecache",
                "startcompcleanup", "resetbase", "browsercache", "errorreports", "updatelogs",
                "defendercache", "cbslogs", "crashdumps" }
        },
        new CleanupProfile
        {
            Name = "Maximaler Speichergewinn", Description = "Zusätzlich Ruhezustand und alte Installationen",
            Level = ProfileLevel.Maximum,
            EnabledItemIds = new List<string> { "usertemp", "wintemp", "recyclebin", "shadercache", "thumbnails", "updatecache",
                "startcompcleanup", "resetbase", "browsercache", "errorreports", "updatelogs",
                "defendercache", "cbslogs", "crashdumps", "hibernate", "windowsupgrade", "restorepoints" }
        },
        new CleanupProfile
        {
            Name = "Alles", Description = "Alle Checkboxen aktivieren",
            Level = ProfileLevel.All,
            EnabledItemIds = new List<string>()
        }
    };

    private int _selectedProfileIndex;
    public int SelectedProfileIndex
    {
        get => _selectedProfileIndex;
        set
        {
            if (SetProperty(ref _selectedProfileIndex, value) && value >= 0 && value < Profiles.Count)
                ApplyProfile(Profiles[value]);
        }
    }

    public CleanupProfile? SelectedProfile =>
        SelectedProfileIndex >= 0 && SelectedProfileIndex < Profiles.Count ? Profiles[SelectedProfileIndex] : null;

    public string RecommendationText
    {
        get
        {
            var rec = SystemInfo.RecommendedProfile;
            var profile = Profiles.FirstOrDefault(p => p.Level == rec);
            var name = profile?.Name ?? rec.ToString();
            return $"Empfohlen: {name}\nGrund: {SystemInfo.RecommendationReason}";
        }
    }

    private bool _isAnalyzing;
    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set => SetProperty(ref _isAnalyzing, value);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            SetProperty(ref _isRunning, value);
            OnPropertyChanged(nameof(CanStartCleanup));
        }
    }

    private string _statusText = "Bereit";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private int _progressPercent;
    public int ProgressPercent
    {
        get => _progressPercent;
        set
        {
            if (SetProperty(ref _progressPercent, value))
                OnPropertyChanged(nameof(ProgressPercentText));
        }
    }
    public string ProgressPercentText => $"{ProgressPercent} %";

    private string _currentAction = string.Empty;
    public string CurrentAction
    {
        get => _currentAction;
        set => SetProperty(ref _currentAction, value);
    }

    private CleanupResult? _lastResult;
    public CleanupResult? LastResult
    {
        get => _lastResult;
        set => SetProperty(ref _lastResult, value);
    }

    public ObservableCollection<LogEntry> LogEntries { get; } = new();
    private int _logGeneration;

    private bool _isDarkTheme;
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
            {
                _settingsService.SetValue("DarkTheme", value);
                _settingsService.Save();
                ApplyTheme();
            }
        }
    }

    private bool _isResultVisible;
    public bool IsResultVisible
    {
        get => _isResultVisible;
        set => SetProperty(ref _isResultVisible, value);
    }

    public bool CanStartCleanup => !IsRunning && HaveCheckedItems();

    private CancellationTokenSource? _cts;

    public async Task AnalyzeAsync()
    {
        if (IsAnalyzing) return;
        IsAnalyzing = true;
        StatusText = "Analysiere...";
        LogEntries.Clear();
        _logGeneration++;

        try
        {
            long totalEstimated = 0;
            foreach (var item in CleanupItems)
            {
                if (!item.IsChecked && !item.Children.Any(c => c.IsChecked)) continue;
                var progress = CreateProgress();
                var size = await _cleanupService.AnalyzeItemAsync(item, progress);
                totalEstimated += size;
                LogEntries.Add(new LogEntry
                {
                    Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                    Message = $"{item.Name}: ~{CleanupItem.FormatSize(size)} erwartet",
                    Level = LogLevel.Info
                });
            }

            StatusText = totalEstimated > 0
                ? $"Erwarteter Speichergewinn: {CleanupItem.FormatSize(totalEstimated)}"
                : "Analyse abgeschlossen";
        }
        catch (Exception ex)
        {
            LogEntries.Add(new LogEntry { Timestamp = DateTime.Now.ToString("HH:mm:ss"), Message = $"Fehler bei Analyse: {ex.Message}", Level = LogLevel.Error });
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    public async Task ExecuteCleanupAsync()
    {
        if (IsRunning) return;

        if (GetSelectedIrreversibleItems().Count > 0)
        {
            var confirmed = await RequestIrreversibleConfirmation();
            if (!confirmed) return;
        }

        await ExecuteCleanupCoreAsync();
    }

    public async Task ExecuteCleanupCoreAsync()
    {
        var beforeFree = SystemInfo.FreeSpace;
        IsRunning = true;
        IsResultVisible = false;
        StatusText = "Bereinigung läuft...";
        _cts = new CancellationTokenSource();
        LogEntries.Clear();
        _logGeneration++;
        LogEntries.Add(new LogEntry { Timestamp = DateTime.Now.ToString("HH:mm:ss"), Message = "Bereinigung gestartet", Level = LogLevel.Info });

        var result = new CleanupResult { FreeSpaceBefore = beforeFree };
        var totalItems = CleanupItems.SelectMany(i => i.Children.Where(c => c.IsChecked)).ToList();
        var totalCount = totalItems.Count;
        var completedItems = 0;

        try
        {
            foreach (var item in CleanupItems)
            {
                if (!item.IsChecked && !item.Children.Any(c => c.IsChecked)) continue;

                CurrentAction = $"Bearbeite: {item.Name}";
                StatusText = $"Bearbeite {item.Name}... ({completedItems + 1}/{totalCount})";
                var progress = CreateProgress();
                var freed = await _cleanupService.ExecuteItemAsync(item, progress, _cts.Token);

                foreach (var child in item.Children.Where(c => c.State != CleanupState.Pending))
                {
                    result.ItemResults.Add(new CleanupItemResult
                    {
                        ItemName = child.Name,
                        ExpectedSize = child.EstimatedSize,
                        ActualFreed = child.ActualFreed,
                        State = child.State
                    });
                    completedItems++;
                    ProgressPercent = (int)((double)completedItems / totalCount * 100);
                }
                result.TotalEstimated += item.GetTotalEstimatedSize();
                result.TotalFreed += freed;
                StatusText = $"{completedItems}/{totalCount} erledigt, freigegeben: {CleanupItem.FormatSize(result.TotalFreed)}";
            }

            SystemInfo = await _cleanupService.GetSystemInfoAsync();
            StatusText = $"Freigegeben: {CleanupItem.FormatSize(result.TotalFreed)}";
            LastResult = result;
            IsResultVisible = true;
            LogEntries.Add(new LogEntry { Timestamp = DateTime.Now.ToString("HH:mm:ss"), Message = $"Bereinigung abgeschlossen: {CleanupItem.FormatSize(result.TotalFreed)} freigegeben", Level = LogLevel.Success });
        }
        catch (OperationCanceledException)
        {
            StatusText = "Bereinigung abgebrochen";
            LogEntries.Add(new LogEntry { Timestamp = DateTime.Now.ToString("HH:mm:ss"), Message = "Bereinigung vom Benutzer abgebrochen", Level = LogLevel.Warning });
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler: {ex.Message}";
            LogEntries.Add(new LogEntry { Timestamp = DateTime.Now.ToString("HH:mm:ss"), Message = $"Fehler: {ex.Message}", Level = LogLevel.Error });
        }
        finally
        {
            IsRunning = false;
            CurrentAction = string.Empty;
            ProgressPercent = 0;
        }
    }

    public void CancelCleanup()
    {
        _cts?.Cancel();
        LogEntries.Add(new LogEntry { Timestamp = DateTime.Now.ToString("HH:mm:ss"), Message = "Abbruch angefordert...", Level = LogLevel.Warning });
    }

    public void ExportLog()
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var path = Path.Combine(desktop, $"WindowsStorageCleaner_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            var lines = LogEntries.Select(l => $"{l.Timestamp} {l.LevelPrefix} {l.Message}");
            File.WriteAllLines(path, lines);
            MessageBox.Show($"Protokoll exportiert nach:\n{path}", "Export erfolgreich", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Export: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ApplyTheme();
    }

    public void ApplyTheme()
    {
        var uri = IsDarkTheme
            ? new Uri("Resources/DarkTheme.xaml", UriKind.Relative)
            : new Uri("Resources/LightTheme.xaml", UriKind.Relative);

        try
        {
            var dict = new ResourceDictionary { Source = uri };
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }
        catch { }
    }

    private void InitializeCleanupItems()
    {
        CleanupItems.Clear();

        CleanupItems.Add(new CleanupItem
        {
            Id = "diskcleanup", Name = "Datenträgerbereinigung", Category = CleanupCategory.DiskCleanup,
            Children =
            {
                CreateChild("tempfiles", "Temporäre Dateien", CleanupAction.CleanMgr,
                    "Löscht temporäre Dateien von Programmen und dem System. Diese werden von Anwendungen während der Installation oder Nutzung erstellt.\nNachteile: Kürzlich installierte Programme müssen ggf. erneut installiert werden. Aktuell laufende Anwendungen könnten kurzzeitig beeinträchtigt sein."),
                CreateChild("internetcache", "Temporäre Internetdateien", CleanupAction.CleanMgr,
                    "Löscht den Cache von Internet Explorer und Edge (legacy). Enthält gespeicherte Webseiten, Bilder und Skripte.\nNachteile: Seiten laden beim ersten Besuch etwas langsamer, da der Cache neu aufgebaut wird."),
                new CleanupItem
                {
                    Id = "recyclebin", Name = "Papierkorb", Action = CleanupAction.CleanMgr,
                    Category = CleanupCategory.DiskCleanup,
                    IsIrreversible = true,
                    InfoText = "Leert den Papierkorb. Enthält zuvor gelöschte Dateien, die noch nicht endgültig entfernt wurden.\nNachteile: Gelöschte Dateien können nicht wiederhergestellt werden.",
                    IrreversibleWarning = "WARNUNG: Diese Aktion kann nicht rückgängig gemacht werden.\nGelöschte Dateien werden endgültig entfernt und können nicht wiederhergestellt werden."
                },
                CreateChild("shadercache", "DirectX Shader Cache", CleanupAction.CleanMgr,
                    "Löscht den DirectX Shader Cache. Enthält kompilierte Shader für Spiele und Grafikprogramme.\nNachteile: Spiele und Grafikprogramme müssen Shader neu kompilieren, was zu kurzen Rucklern beim ersten Start führen kann."),
                CreateChild("thumbnails", "Miniaturansichten", CleanupAction.CleanMgr,
                    "Löscht den Miniaturansichten-Cache. Windows speichert kleine Vorschaubilder von Dateien (Bilder, Videos etc.).\nNachteile: Der Ordner-Explorer muss Thumbnails neu generieren, was kurzzeitig etwas langsamer sein kann."),
                CreateChild("errorreports", "Windows Fehlerberichte", CleanupAction.CleanMgr,
                    "Löscht archivierte Windows-Fehlerberichte und Lösungsvorschläge. Kann mehrere GB belegen, wenn viele Abstürze protokolliert wurden.\nNachteile: Fehlerdiagnose bei zukünftigen Problemen wird erschwert."),
                CreateChild("deliveryopt", "Delivery Optimization Dateien", CleanupAction.CleanMgr,
                    "Löscht zwischengespeicherte Update-Pakete von der Delivery Optimization (Peer-to-Peer Update-Verteilung).\nNachteile: Updates müssen erneut heruntergeladen werden, falls sie noch benötigt werden."),
                CreateChild("updatelogs", "Alte Update-Protokolle", CleanupAction.CleanMgr,
                    "Löscht alte Windows Update-Protokolldateien (*.log).\nNachteile: Keine, die Protokolle werden nicht mehr benötigt."),
                CreateChild("driverpacks", "Geräte- und Treiberpakete", CleanupAction.CleanMgr,
                    "Löscht alte nicht mehr benötigte Treiberpakete. Windows behält mehrere Versionen von Gerätetreibern.\nNachteile: Ältere Treiberversionen können nicht wiederhergestellt werden, falls ein neuer Treiber Probleme macht."),
                CreateChild("windowsupgrade", "Windows Upgrade Dateien", CleanupAction.CleanMgr,
                    "Löscht Dateien von früheren Windows-Installationen (Windows.old) nach einem größeren Update.\nNachteile: Ein Zurücksetzen auf die alte Windows-Version ist danach nicht mehr möglich."),
                CreateChild("memorydumps", "Speicherabbilder", CleanupAction.CleanMgr,
                    "Löscht Systemabsturz-Speicherabbilder (Dump-Dateien). Diese werden bei Blue Screens (BSOD) erstellt.\nNachteile: Absturzanalyse durch Experten wird erschwert."),
                CreateChild("chkdsk", "Chkdsk-Dateifragmente", CleanupAction.CleanMgr,
                    "Löscht temporäre Dateifragmente von der Datenträgerprüfung (chkdsk).\nNachteile: Keine, diese Fragmente werden nicht mehr benötigt."),
            }
        });

        CleanupItems.Add(new CleanupItem
        {
            Id = "componentcleanup", Name = "Windows Komponentenbereinigung", Category = CleanupCategory.ComponentCleanup,
            Children =
            {
                CreateChild("startcompcleanup", "StartComponentCleanup", CleanupAction.DISM,
                    "Führt DISM StartComponentCleanup aus. Entfernt alte Versionen von Windows-Komponenten aus dem Side-by-Side Store (WinSxS).\nNachteile: Updates können nicht mehr deinstalliert werden."),
                new CleanupItem
                {
                    Id = "resetbase", Name = "ResetBase", Action = CleanupAction.DISM,
                    Category = CleanupCategory.ComponentCleanup,
                    InfoText = "Führt DISM /ResetBase aus. Komprimiert den WinSxS-Ordner, indem alle alten Komponentenversionen endgültig entfernt werden.\nNachteile: Nach ResetBase können Windows-Updates und Service Packs NICHT MEHR deinstalliert werden.",
                    IsIrreversible = true,
                    IrreversibleWarning = "WARNUNG: Diese Aktion kann nicht rückgängig gemacht werden.\nNach ResetBase können Windows-Updates nicht mehr deinstalliert werden."
                }
            }
        });

        CleanupItems.Add(new CleanupItem
        {
            Id = "tempfiles", Name = "Temporäre Dateien", Category = CleanupCategory.TempFiles,
            Children =
            {
                new CleanupItem
                {
                    Id = "usertemp", Name = "Benutzer TEMP", Action = CleanupAction.DeleteDirectory,
                    ActionData = "%TEMP%", Category = CleanupCategory.TempFiles,
                    InfoText = "Löscht den Inhalt des Benutzer-TEMP-Ordners (%TEMP%). Dieser Ordner enthält temporäre Dateien von Anwendungen.\nNachteile: Aktuell geöffnete Programme könnten kurzzeitig beeinträchtigt sein. Empfohlen wird ein Neustart vor der Bereinigung."
                },
                new CleanupItem
                {
                    Id = "wintemp", Name = "Windows TEMP", Action = CleanupAction.DeleteDirectory,
                    ActionData = "%SystemRoot%\\Temp", Category = CleanupCategory.TempFiles,
                    InfoText = "Löscht den Inhalt des Windows-TEMP-Ordners. Enthält systemweite temporäre Dateien.\nNachteile: Aktuell laufende Systemprozesse könnten kurzzeitig beeinträchtigt sein."
                }
            }
        });

        CleanupItems.Add(new CleanupItem
        {
            Id = "updatecache", Name = "Windows Update Cache", Action = CleanupAction.StopServices,
            Category = CleanupCategory.UpdateCache,
            InfoText = "Stoppt den Update-Dienst und löscht den Windows Update-Cache. Danach wird der Dienst neu gestartet. Enthält heruntergeladene Update-Dateien.\nNachteile: Windows sucht nach dem nächsten Neustart erneut nach Updates und lädt diese ggf. neu herunter."
        });

        CleanupItems.Add(new CleanupItem
        {
            Id = "hibernate", Name = "Ruhezustand", Action = CleanupAction.RunCommand,
            ActionData = "powercfg -h off", Category = CleanupCategory.Hibernate,
            IsIrreversible = true,
            InfoText = "Deaktiviert den Ruhezustand und löscht die Datei hiberfil.sys. Der Ruhezustand speichert den Arbeitsspeicher auf die Festplatte, um den PC komplett auszuschalten und später fortzusetzen.\nNachteile: Der Ruhezustand und der Hybrid-Schlafmodus stehen nicht mehr zur Verfügung. Der PC kann nicht mehr aus dem vollständigen Ausschaltzustand mit geöffneten Programmen fortgesetzt werden.",
            IrreversibleWarning = "WARNUNG: Diese Aktion kann nicht rückgängig gemacht werden.\nDer Ruhezustand wird deaktiviert und die hiberfil.sys gelöscht."
        });

        CleanupItems.Add(new CleanupItem
        {
            Id = "additional", Name = "Weitere Bereinigungen", Category = CleanupCategory.Additional,
            Children =
            {
                new CleanupItem
                {
                    Id = "browsercache", Name = "Browser Cache", Category = CleanupCategory.BrowserCache,
                    Children =
                    {
                        new CleanupItem { Id = "edgecache", Name = "Microsoft Edge", Action = CleanupAction.DeleteDirectory, ActionData = "%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\Default\\Cache", Category = CleanupCategory.BrowserCache, InfoText = "Löscht den Cache von Microsoft Edge. Enthält zwischengespeicherte Webseiten und Dateien.\nNachteile: Seiten laden kurzzeitig langsamer, Anmeldeinformationen in manchen Webseiten könnten zurückgesetzt werden." },
                        new CleanupItem { Id = "chromecache", Name = "Google Chrome", Action = CleanupAction.DeleteDirectory, ActionData = "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default\\Cache", Category = CleanupCategory.BrowserCache, InfoText = "Löscht den Cache von Google Chrome. Enthält zwischengespeicherte Webseiten und Dateien.\nNachteile: Seiten laden kurzzeitig langsamer, Anmeldeinformationen in manchen Webseiten könnten zurückgesetzt werden." },
                        new CleanupItem { Id = "firefoxcache", Name = "Firefox", Action = CleanupAction.DeleteFiles, ActionData = "%LOCALAPPDATA%\\Mozilla\\Firefox\\Profiles\\*\\cache2\\*|%APPDATA%\\Mozilla\\Firefox\\Profiles\\*\\cache2\\*", Category = CleanupCategory.BrowserCache, InfoText = "Löscht den Cache von Firefox. Enthält zwischengespeicherte Webseiten und Dateien.\nNachteile: Seiten laden kurzzeitig langsamer, Anmeldeinformationen in manchen Webseiten könnten zurückgesetzt werden." }
                    }
                },
                new CleanupItem { Id = "eventlogs", Name = "Windows Ereignisprotokolle", Action = CleanupAction.ClearEventLog, Category = CleanupCategory.SystemLogs, InfoText = "Leert die Windows-Ereignisprotokolle (System, Anwendung, Sicherheit). Diese enthalten systemweite Ereignisaufzeichnungen.\nNachteile: Die Verlaufsdaten für die Problembehandlung gehen verloren. Zukünftige Ereignisse werden weiterhin protokolliert." },
                new CleanupItem { Id = "defendercache", Name = "Defender Cache", Action = CleanupAction.RunCommand, ActionData = "MpCmdRun.exe -RemoveDefinitions -All", Category = CleanupCategory.Additional, InfoText = "Setzt die Windows Defender Virendefinitionen zurück. Kann helfen, wenn der Defender Speicherplatz belegt oder Probleme macht.\nNachteile: Der Defender muss die neuesten Virendefinitionen neu herunterladen. Der PC ist kurzzeitig weniger geschützt." },
                new CleanupItem { Id = "crashdumps", Name = "Crash Dumps", Action = CleanupAction.DeleteFiles, ActionData = "%SystemRoot%\\Minidump\\*.*|%LOCALAPPDATA%\\CrashDumps\\*.*", Category = CleanupCategory.Additional, InfoText = "Löscht Absturzprotokolle von Programmen und Systemabstürzen (Minidump).\nNachteile: Fehlerdiagnose bei zukünftigen Abstürzen wird erschwert." },
                new CleanupItem { Id = "cbslogs", Name = "Alte CBS Logs", Action = CleanupAction.DeleteFiles, ActionData = "%SystemRoot%\\Logs\\CBS\\CBS*.log", Category = CleanupCategory.Additional, InfoText = "Löscht alte Component-Based Servicing (CBS) Protokolldateien. Diese werden bei Windows Update und DISM erstellt.\nNachteile: Keine, da nur alte, nicht mehr benötigte Log-Dateien gelöscht werden." },
                new CleanupItem { Id = "oldwindows", Name = "Alte Windows Installationen", Action = CleanupAction.RunCommand, ActionData = "dism /online /Cleanup-OS /ScratchDir:%TEMP%", Category = CleanupCategory.Additional, IsIrreversible = true, InfoText = "Entfernt alte Windows-Installationen (Windows.old). Dies sind Überreste von früheren Windows-Upgrades.\nNachteile: Ein Zurücksetzen auf die vorherige Windows-Version ist danach NICHT MEHR möglich.", IrreversibleWarning = "WARNUNG: Diese Aktion kann nicht rückgängig gemacht werden.\nAlte Windows-Installationen werden endgültig entfernt." },
                new CleanupItem { Id = "deliveryoptcache", Name = "Delivery Optimization Cache", Action = CleanupAction.DeleteDirectory, ActionData = "%SystemRoot%\\ServiceState\\DeliveryOptimization\\Cache", Category = CleanupCategory.Additional, InfoText = "Löscht den Cache der Delivery Optimization (P2P Update-Verteilung).\nNachteile: Updates für andere PCs im Netzwerk können nicht mehr von diesem PC bezogen werden." },
                new CleanupItem { Id = "restorepoints", Name = "Wiederherstellungspunkte löschen", Action = CleanupAction.RunCommand, ActionData = "vssadmin delete shadows /all /quiet", Category = CleanupCategory.Additional, IsIrreversible = true, InfoText = "Löscht alle Systemwiederherstellungspunkte. Diese enthalten Sicherungen von Systemdateien und der Registry.\nNachteile: Nach einem Systemfehler kann nicht auf einen früheren Zustand zurückgesetzt werden. Neue Wiederherstellungspunkte werden ab jetzt automatisch erstellt.", IrreversibleWarning = "WARNUNG: Diese Aktion kann nicht rückgängig gemacht werden.\nAlle Systemwiederherstellungspunkte werden gelöscht." },
                new CleanupItem { Id = "compact", Name = "Speicheroptimierung starten", Action = CleanupAction.RunPowerShell, ActionData = "compact /compactos:always", Category = CleanupCategory.Additional, InfoText = "Aktiviert die NTFS-Komprimierung für Systemdateien mit compact /compactos:always. Reduziert den Speicherverbrauch durch Komprimierung.\nNachteile: Die Systemleistung kann minimal sinken, da Dateien beim Zugriff dekomprimiert werden müssen. Der Vorgang dauert mehrere Minuten." }
            }
        });

        foreach (var item in CleanupItems)
        {
            foreach (var child in item.Children)
            {
                child.Parent = item;
                if (child.HasChildren)
                {
                    foreach (var grandchild in child.Children)
                        grandchild.Parent = child;
                }
            }
        }
    }

    private static CleanupItem CreateChild(string id, string name, CleanupAction action, string infoText = "")
    {
        return new CleanupItem
        {
            Id = id, Name = name, Action = action,
            Category = CleanupCategory.DiskCleanup,
            InfoText = infoText
        };
    }

    private void ApplyProfile(CleanupProfile profile)
    {
        if (profile.Level == ProfileLevel.None)
        {
            foreach (var item in CleanupItems)
            {
                if (item.HasChildren)
                    foreach (var child in item.Children)
                        child.IsChecked = false;
                else
                    item.IsChecked = false;
            }
        }
        else if (profile.Level == ProfileLevel.All)
        {
            foreach (var item in CleanupItems)
            {
                if (item.HasChildren)
                    foreach (var child in item.Children)
                        child.IsChecked = true;
                else
                    item.IsChecked = true;
            }
        }
        else
        {
            foreach (var item in CleanupItems)
            {
                if (item.HasChildren)
                {
                    foreach (var child in item.Children)
                    {
                        child.IsChecked = profile.EnabledItemIds.Contains(child.Id);
                    }
                }
                else
                {
                    item.IsChecked = profile.EnabledItemIds.Contains(item.Id);
                }
            }
        }
        LogEntries.Add(new LogEntry { Timestamp = DateTime.Now.ToString("HH:mm:ss"), Message = $"Profil geladen: {profile.Name}", Level = LogLevel.Info });
    }

    private void ApplyProfileRecommendation()
    {
        var rec = SystemInfo.RecommendedProfile;
        var idx = Profiles.FindIndex(p => p.Level == rec);
        if (idx >= 0)
        {
            _selectedProfileIndex = idx;
            OnPropertyChanged(nameof(SelectedProfileIndex));
            var profile = Profiles[idx];
            ApplyProfile(profile);
            LogEntries.Add(new LogEntry { Timestamp = DateTime.Now.ToString("HH:mm:ss"), Message = $"Automatische Empfehlung: {profile.Name}", Level = LogLevel.Info });
        }
    }

    private List<CleanupItem> GetSelectedIrreversibleItems()
    {
        var items = new List<CleanupItem>();
        foreach (var item in CleanupItems)
        {
            CollectIrreversible(item, items);
        }
        return items;
    }

    private static void CollectIrreversible(CleanupItem item, List<CleanupItem> items)
    {
        if (item.IsIrreversible && item.IsChecked)
            items.Add(item);
        if (item.HasChildren)
        {
            foreach (var child in item.Children)
                CollectIrreversible(child, items);
        }
    }

    private bool HaveCheckedItems()
    {
        return CleanupItems.Any(i => i.IsChecked || i.Children.Any(c => c.IsChecked));
    }

    private async Task<bool> RequestIrreversibleConfirmation()
    {
        var items = GetSelectedIrreversibleItems();
        var combinedWarning = string.Join("\n", items.Select(i =>
        {
            var detail = i.IrreversibleWarning.Replace("WARNUNG: Diese Aktion kann nicht rückgängig gemacht werden.\n", "");
            return $"  - {i.Name}: {detail}";
        }));

        var tcs = new TaskCompletionSource<bool>();
        var vm = new ConfirmViewModel
        {
            ItemName = $"Folgende {items.Count} Aktionen sind irreversibel",
            WarningText = $"Diese Aktionen können nicht rückgängig gemacht werden:\n\n{combinedWarning}"
        };

        Views.ConfirmDialog? dialog = null;
        vm.Confirmed += (s, e) => tcs.TrySetResult(e);
        vm.RequestClose += () => dialog?.Close();

        Application.Current.Dispatcher.Invoke(() =>
        {
            dialog = new Views.ConfirmDialog
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow,
                IsDarkTheme = IsDarkTheme
            };
            dialog.ShowDialog();
            tcs.TrySetResult(vm.IsConfirmed);
        });

        return await tcs.Task;
    }

    private IProgress<LogEntry> CreateProgress()
    {
        var generation = _logGeneration;
        return new Progress<LogEntry>(entry =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (generation != _logGeneration) return;
                LogEntries.Add(entry);
                if (LogEntries.Count > 1000)
                    LogEntries.RemoveAt(0);
            });
        });
    }

    private void LoadSettings()
    {
        var saved = _settingsService.GetValue("SelectedProfile", 0);
        if (Profiles.Count == 6 && saved >= 0 && saved <= 3)
            saved += 1;
        SelectedProfileIndex = Math.Min(saved, Profiles.Count - 1);
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    private void DetectWindowsTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
                _isDarkTheme = value == 0;
        }
        catch { }
    }

    public void SaveSettings()
    {
        _settingsService.SetValue("DarkTheme", IsDarkTheme);
        _settingsService.SetValue("SelectedProfile", SelectedProfileIndex);
        _settingsService.Save();
    }
}
