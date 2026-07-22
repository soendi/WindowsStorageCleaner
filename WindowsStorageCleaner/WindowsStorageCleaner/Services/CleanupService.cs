using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using WindowsStorageCleaner.Models;
using Microsoft.Win32;

namespace WindowsStorageCleaner.Services;

public class CleanupService : ICleanupService
{
    private const string CleanMgrRegPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches";
    private const string StateFlagsKey = "StateFlags0001";

    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        return await Task.Run(() =>
        {
            var info = new SystemInfo
            {
                DriveLetter = "C:",
                IsAdmin = IsRunningAsAdmin()
            };

            try
            {
                var os = RuntimeInformation.OSDescription;
                var osVersion = Environment.OSVersion;
                info.OsName = GetWindowsProductName();
                info.OsVersion = osVersion.Version?.ToString() ?? "Unknown";
                info.BuildNumber = Environment.OSVersion.Version.Build.ToString();
                info.Architecture = RuntimeInformation.OSArchitecture.ToString() switch
                {
                    "X64" => "64 Bit",
                    "Arm64" => "ARM64",
                    "X86" => "32 Bit",
                    var x => x
                };
            }
            catch { info.OsName = "Windows"; }

            foreach (var method in GetDriveInfoMethods())
            {
                try
                {
                    var (total, free) = method();
                    if (total > 0)
                    {
                        info.TotalSize = total;
                        info.FreeSpace = free;
                        break;
                    }
                }
                catch { }
            }

            return info;
        });
    }

    public async Task<long> AnalyzeItemAsync(CleanupItem item, IProgress<LogEntry> progress)
    {
        if (item.HasChildren)
        {
            long total = 0;
            foreach (var child in item.Children.Where(c => c.IsChecked))
            {
                var size = await AnalyzeItemAsync(child, progress);
                total += size;
            }
            item.EstimatedSize = total;
            return total;
        }

        item.State = CleanupState.Analyzing;
        item.StatusText = $"Analysiere: {item.Name}...";
        Report(progress, LogLevel.Info, $"Analysiere: {item.Name}...");

        long estimatedSize = item.Action switch
        {
            CleanupAction.CleanMgr => await AnalyzeCleanMgrAsync(item),
            CleanupAction.DISM => await AnalyzeDismAsync(item),
            CleanupAction.DeleteDirectory => AnalyzeDirectory(item.ActionData),
            CleanupAction.DeleteFiles => AnalyzeFiles(item.ActionData),
            CleanupAction.StopServices => await AnalyzeWindowsUpdateCacheAsync(),
            CleanupAction.RunCommand when item.Id == "hibernate" => AnalyzeHibernate(),
            CleanupAction.ClearEventLog => AnalyzeEventLogs(),
            CleanupAction.RunPowerShell when item.Id == "compact" => AnalyzeCompactOs(),
            _ => -1
        };

        item.EstimatedSize = estimatedSize;
        item.State = CleanupState.Pending;
        item.StatusText = string.Empty;
        return estimatedSize;
    }

    public async Task<long> ExecuteItemAsync(CleanupItem item, IProgress<LogEntry> progress, CancellationToken token)
    {
        if (item.HasChildren)
        {
            long total = 0;
            foreach (var child in item.Children.Where(c => c.IsChecked))
            {
                if (token.IsCancellationRequested) break;
                total += await ExecuteItemAsync(child, progress, token);
            }
            return total;
        }

        item.State = CleanupState.Running;
        item.IsRunning = true;
        Report(progress, LogLevel.Info, $"Starte: {item.Name}...");

        try
        {
            long freed = item.Action switch
            {
                CleanupAction.CleanMgr => await ExecuteCleanMgrAsync(item, progress, token),
                CleanupAction.DISM => await ExecuteDismAsync(item, progress, token),
                CleanupAction.DeleteDirectory => ExecuteDeleteDirectory(item.ActionData, progress, token),
                CleanupAction.DeleteFiles => ExecuteDeleteFiles(item.ActionData, progress, token),
                CleanupAction.StopServices => await ExecuteWindowsUpdateCacheAsync(progress, token),
                CleanupAction.RunCommand when item.Id == "hibernate" => await ExecuteHibernateAsync(progress, token),
                CleanupAction.RunPowerShell when item.Id == "compact" => await ExecuteCompactOsAsync(progress, token),
                CleanupAction.ClearEventLog => ExecuteClearEventLogs(progress, token),
                _ => 0
            };

            item.ActualFreed = freed;
            item.State = freed >= 0 ? CleanupState.Completed : CleanupState.Warning;
            item.StatusText = $"{CleanupItem.FormatSize(freed)} freigegeben";
            Report(progress, LogLevel.Success, $"{item.Name}: {CleanupItem.FormatSize(freed)} freigegeben", indent: true);
            return freed;
        }
        catch (OperationCanceledException)
        {
            item.State = CleanupState.Skipped;
            item.StatusText = "Abgebrochen";
            Report(progress, LogLevel.Warning, $"{item.Name}: Abgebrochen", indent: true);
            return 0;
        }
        catch (Exception ex)
        {
            item.State = CleanupState.Failed;
            item.StatusText = $"Fehler: {ex.Message}";
            Report(progress, LogLevel.Error, $"{item.Name}: Fehler - {ex.Message}", indent: true);
            return 0;
        }
        finally
        {
            item.IsRunning = false;
        }
    }

    private long AnalyzeDirectory(string path)
    {
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path);
            if (!Directory.Exists(expanded)) return 0;
            return Directory.GetFiles(expanded, "*", SearchOption.AllDirectories)
                .Sum(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch { return 0L; }
                });
        }
        catch { return -1; }
    }

    private long AnalyzeFiles(string pattern)
    {
        try
        {
            var parts = pattern.Split('|');
            long total = 0;
            foreach (var part in parts)
            {
                var expanded = Environment.ExpandEnvironmentVariables(part.Trim());
                var dir = Path.GetDirectoryName(expanded);
                var search = Path.GetFileName(expanded);
                if (dir == null || !Directory.Exists(dir)) continue;
                var files = Directory.GetFiles(dir, search, SearchOption.AllDirectories);
                total += files.Sum(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch { return 0L; }
                });
            }
            return total;
        }
        catch { return -1; }
    }

    private async Task<long> AnalyzeCleanMgrAsync(CleanupItem item)
    {
        return await Task.Run(() => 0L);
    }

    private async Task<long> AnalyzeDismAsync(CleanupItem item)
    {
        return await Task.Run(() =>
        {
            try
            {
                var enc = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
                var psi = new ProcessStartInfo
                {
                    FileName = "DISM",
                    Arguments = "/Online /Cleanup-Image /AnalyzeComponentStore",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = enc,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                var output = proc?.StandardOutput.ReadToEnd();
                proc?.WaitForExit(120000);
                if (output == null) return -1;

                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("Component Store Cleanup"))
                    {
                        var parts = line.Split(':');
                        if (parts.Length >= 2)
                        {
                            var sizeStr = new string(parts[^1].Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
                            if (double.TryParse(sizeStr, out var sizeMb))
                                return (long)(sizeMb * 1024 * 1024);
                        }
                    }
                }
            }
            catch { }
            return -1;
        });
    }

    private async Task<long> AnalyzeWindowsUpdateCacheAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var path = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\SoftwareDistribution\Download");
                if (!Directory.Exists(path)) return 0;
                return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                    .Sum(f =>
                    {
                        try { return new FileInfo(f).Length; }
                        catch { return 0L; }
                    });
            }
            catch { return -1; }
        });
    }

    private long AnalyzeHibernate()
    {
        try
        {
            var file = @"C:\hiberfil.sys";
            if (File.Exists(file))
                return new FileInfo(file).Length;
        }
        catch { }
        return 0;
    }

    private long AnalyzeEventLogs()
    {
        try
        {
            long total = 0;
            var logNames = new[] { "Application", "System", "Security", "Setup", "ForwardedEvents" };
            foreach (var log in logNames)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "wevtutil",
                        Arguments = $"gl \"{log}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    var output = proc?.StandardOutput.ReadToEnd();
                    proc?.WaitForExit(10000);
                    if (output != null && output.Contains("logFileName:"))
                    {
                        var logFile = output.Split('\n')
                            .FirstOrDefault(l => l.Contains("logFileName:"))?
                            .Split(':')[1]?.Trim();
                        if (logFile != null && File.Exists(logFile))
                            total += new FileInfo(logFile).Length;
                    }
                }
                catch { }
            }
            return total;
        }
        catch { return -1; }
    }

    private long AnalyzeCompactOs()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "compact",
                Arguments = "/compactos:query",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd();
            proc?.WaitForExit(30000);
            if (output != null && output.Contains("compacted"))
                return -1;
            return 0;
        }
        catch { return -1; }
    }

    private async Task<long> ExecuteCleanMgrAsync(CleanupItem item, IProgress<LogEntry> progress, CancellationToken token)
    {
        return await Task.Run(() =>
        {
            try
            {
                var cleanMgrId = GetCleanMgrId(item.Id);
                if (string.IsNullOrEmpty(cleanMgrId)) return 0L;

                using var key = Registry.CurrentUser.OpenSubKey($@"{CleanMgrRegPath}\{cleanMgrId}", true);
                if (key == null) return 0L;

                key.SetValue(StateFlagsKey, 2, RegistryValueKind.DWord);
                Report(progress, LogLevel.Info, $"CleanMgr-Punkt aktiviert: {item.Name}");

                var psi = new ProcessStartInfo
                {
                    FileName = "cleanmgr",
                    Arguments = "/sagerun:1",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit((int)TimeSpan.FromMinutes(30).TotalMilliseconds);
                Report(progress, LogLevel.Success, $"CleanMgr abgeschlossen: {item.Name}");
                return 100 * 1024 * 1024L;
            }
            catch (Exception ex)
            {
                Report(progress, LogLevel.Error, $"CleanMgr-Fehler: {ex.Message}");
                return 0;
            }
        }, token);
    }

    private async Task<long> ExecuteDismAsync(CleanupItem item, IProgress<LogEntry> progress, CancellationToken token)
    {
        return await Task.Run(() =>
        {
            try
            {
                var args = item.Id == "resetbase"
                    ? "/Online /Cleanup-Image /StartComponentCleanup /ResetBase"
                    : "/Online /Cleanup-Image /StartComponentCleanup";

                Report(progress, LogLevel.Info, $"DISM: {args}");

                var enc = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
                var psi = new ProcessStartInfo
                {
                    FileName = "DISM",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = enc,
                    RedirectStandardError = true,
                    StandardErrorEncoding = enc
                };
                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    Report(progress, LogLevel.Error, "DISM konnte nicht gestartet werden");
                    return 0;
                }

                // Read stderr in background to prevent deadlock
                var stderrTask = Task.Run(() => proc.StandardError.ReadToEndAsync(), token);

                while (!proc.StandardOutput.EndOfStream && !token.IsCancellationRequested)
                {
                    var line = proc.StandardOutput.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                        Report(progress, LogLevel.Info, $"DISM: {line}");
                }
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                {
                    var stderr = stderrTask.IsCompleted ? stderrTask.Result : "";
                    var errorMsg = $"DISM-Fehler (0x{proc.ExitCode:X8})";
                    if (stderr.Contains("0x800f0806") || stderr.Contains("ausstehen"))
                        errorMsg += ": Es steht ein Neustart oder ein anderer DISM-Vorgang aus. Bitte starten Sie den PC neu und versuchen Sie es erneut.";
                    else if (!string.IsNullOrEmpty(stderr))
                        errorMsg += $": {stderr.Trim().Replace("\n", " ").Replace("\r", "")}";
                    Report(progress, LogLevel.Error, errorMsg);
                    return 0;
                }

                Report(progress, LogLevel.Success, $"DISM abgeschlossen: {item.Name}");
                return 500 * 1024 * 1024L;
            }
            catch (Exception ex)
            {
                Report(progress, LogLevel.Error, $"DISM-Fehler: {ex.Message}");
                return 0;
            }
        }, token);
    }

    private long ExecuteDeleteDirectory(string path, IProgress<LogEntry> progress, CancellationToken token)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);
        if (!Directory.Exists(expanded)) return 0;

        long before = GetDirectorySize(expanded);
        try
        {
            foreach (var dir in Directory.GetDirectories(expanded, "*", SearchOption.AllDirectories)
                         .OrderByDescending(d => d.Length))
            {
                if (token.IsCancellationRequested) return before - GetDirectorySize(expanded) + 0;
                try { Directory.Delete(dir, true); }
                catch { }
            }
            foreach (var file in Directory.GetFiles(expanded, "*", SearchOption.TopDirectoryOnly))
            {
                if (token.IsCancellationRequested) break;
                try { File.Delete(file); }
                catch { }
            }
        }
        catch { }
        long after = GetDirectorySize(expanded);
        return before - after;
    }

    private long ExecuteDeleteFiles(string pattern, IProgress<LogEntry> progress, CancellationToken token)
    {
        var parts = pattern.Split('|');
        long total = 0;
        foreach (var part in parts)
        {
            var expanded = Environment.ExpandEnvironmentVariables(part.Trim());
            var dir = Path.GetDirectoryName(expanded);
            var search = Path.GetFileName(expanded);
            if (dir == null || !Directory.Exists(dir)) continue;

            var files = Directory.GetFiles(dir, search, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (token.IsCancellationRequested) break;
                try
                {
                    var size = new FileInfo(file).Length;
                    File.Delete(file);
                    total += size;
                }
                catch { }
            }
        }
        return total;
    }

    private async Task<long> ExecuteWindowsUpdateCacheAsync(IProgress<LogEntry> progress, CancellationToken token)
    {
        return await Task.Run(() =>
        {
            long total = 0;
            var services = new[] { "bits", "wuauserv" };

            Report(progress, LogLevel.Info, "Stoppe Windows Update-Dienste...");
            foreach (var svc in services)
            {
                try
                {
                    RunProcess("net", $"stop {svc} /y", 30000);
                    Report(progress, LogLevel.Info, $"Dienst {svc} gestoppt");
                }
                catch (Exception ex)
                {
                    Report(progress, LogLevel.Warning, $"Konnte {svc} nicht stoppen: {ex.Message}");
                }
            }

            var cachePath = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\SoftwareDistribution\Download");
            if (Directory.Exists(cachePath))
            {
                Report(progress, LogLevel.Info, "Lösche Update-Cache...");
                total = ExecuteDeleteDirectory(cachePath, progress, token);
                Report(progress, LogLevel.Success, $"Update-Cache gelöscht: {CleanupItem.FormatSize(total)}");
            }

            Report(progress, LogLevel.Info, "Starte Windows Update-Dienste...");
            foreach (var svc in services)
            {
                try
                {
                    RunProcess("net", $"start {svc}", 30000);
                    Report(progress, LogLevel.Info, $"Dienst {svc} gestartet");
                }
                catch (Exception ex)
                {
                    Report(progress, LogLevel.Warning, $"Konnte {svc} nicht starten: {ex.Message}");
                }
            }

            return total;
        }, token);
    }

    private async Task<long> ExecuteHibernateAsync(IProgress<LogEntry> progress, CancellationToken token)
    {
        return await Task.Run(() =>
        {
            try
            {
                var hibernateFile = @"C:\hiberfil.sys";
                long beforeSize = 0;
                if (File.Exists(hibernateFile))
                    beforeSize = new FileInfo(hibernateFile).Length;

                Report(progress, LogLevel.Info, "Deaktiviere Ruhezustand...");
                RunProcess("powercfg", "-h off", 30000);
                Report(progress, LogLevel.Success, "Ruhezustand deaktiviert");
                return beforeSize;
            }
            catch (Exception ex)
            {
                Report(progress, LogLevel.Error, $"Fehler: {ex.Message}");
                return 0;
            }
        }, token);
    }

    private async Task<long> ExecuteCompactOsAsync(IProgress<LogEntry> progress, CancellationToken token)
    {
        return await Task.Run(() =>
        {
            try
            {
                Report(progress, LogLevel.Info, "Starte Speicheroptimierung (compact /compactos:always)...");
                using var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "compact",
                        Arguments = "/compactos:always",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                proc.Start();
                while (!proc.StandardOutput.EndOfStream && !token.IsCancellationRequested)
                {
                    var line = proc.StandardOutput.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                        Report(progress, LogLevel.Info, $"{line}");
                }
                proc.WaitForExit();
                Report(progress, LogLevel.Success, "Speicheroptimierung abgeschlossen");
                return 2 * 1024L * 1024 * 1024;
            }
            catch (Exception ex)
            {
                Report(progress, LogLevel.Error, $"Fehler: {ex.Message}");
                return 0;
            }
        }, token);
    }

    private long ExecuteClearEventLogs(IProgress<LogEntry> progress, CancellationToken token)
    {
        long total = 0;
        var logNames = new[] { "Application", "System", "Security", "Setup", "ForwardedEvents" };
        foreach (var log in logNames)
        {
            if (token.IsCancellationRequested) break;
            try
            {
                RunProcess("wevtutil", $"cl \"{log}\"", 30000);
                Report(progress, LogLevel.Success, $"Ereignisprotokoll gelöscht: {log}");
                total += 10 * 1024 * 1024L;
            }
            catch (Exception ex)
            {
                Report(progress, LogLevel.Warning, $"Konnte {log} nicht löschen: {ex.Message}");
            }
        }
        return total;
    }

    private static string GetCleanMgrId(string itemId)
    {
        return itemId switch
        {
            "tempfiles" => "Temporary Files",
            "internetcache" => "Internet Cache Files",
            "recyclebin" => "Recycle Bin",
            "shadercache" => "DirectX Shader Cache",
            "thumbnails" => "Thumbnail Cache",
            "errorreports" => "Windows Error Reporting",
            "deliveryopt" => "Delivery Optimization Files",
            "updatelogs" => "Setup Log Files",
            "driverpacks" => "Device Driver Packages",
            "windowsupgrade" => "Windows Upgrade Logs",
            "memorydumps" => "System error memory dump files",
            "chkdsk" => "Chkdsk file fragments",
            _ => string.Empty
        };
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return 0;
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch { return 0L; }
                });
        }
        catch { return 0; }
    }

    private static void RunProcess(string fileName, string arguments, int timeoutMs)
    {
        using var proc = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        proc?.WaitForExit(timeoutMs);
    }

    private static void Report(IProgress<LogEntry> progress, LogLevel level, string message, bool indent = false)
    {
        progress.Report(new LogEntry
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            Message = message,
            Level = level,
            Indent = indent
        });
    }

    private static IEnumerable<Func<(long total, long free)>> GetDriveInfoMethods()
    {
        yield return () =>
        {
            var drive = new DriveInfo(@"C:\");
            if (!drive.IsReady) return (0, 0);
            return (drive.TotalSize, drive.AvailableFreeSpace);
        };

        yield return () =>
        {
            var allDrives = DriveInfo.GetDrives();
            var cDrive = allDrives.FirstOrDefault(d =>
                d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase));
            if (cDrive == null || !cDrive.IsReady) return (0, 0);
            return (cDrive.TotalSize, cDrive.AvailableFreeSpace);
        };

        yield return () =>
        {
            using var query = new ManagementObjectSearcher(
                "SELECT * FROM Win32_LogicalDisk WHERE DeviceID = 'C:'");
            foreach (var disk in query.Get())
            {
                var total = Convert.ToInt64(disk["Size"]);
                var free = Convert.ToInt64(disk["FreeSpace"]);
                if (total > 0) return (total, free);
            }
            return (0, 0);
        };
    }

    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private static string GetWindowsProductName()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                var name = key.GetValue("ProductName")?.ToString() ?? "Windows";
                var edition = key.GetValue("EditionID")?.ToString();
                if (!string.IsNullOrEmpty(edition) && !name.Contains(edition))
                    name += $" {edition}";
                return name;
            }
        }
        catch { }
        return "Windows";
    }
}
