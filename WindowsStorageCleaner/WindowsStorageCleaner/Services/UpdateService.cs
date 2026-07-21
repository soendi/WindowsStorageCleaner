using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace WindowsStorageCleaner.Services;

public class UpdateService
{
    private const string Owner = "soendi";
    private const string Repo = "WindowsStorageCleaner";
    private const string InstallerName = "WindowsStorageCleaner_Setup.msi";

    private static readonly string VersionUrl =
        $"https://raw.githubusercontent.com/{Owner}/{Repo}/master/WindowsStorageCleaner/WindowsStorageCleaner/version.json";

    private static readonly string ReleasesApiUrl =
        $"https://api.github.com/repos/{Owner}/{Repo}/releases?per_page=10";

    public Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

    public event Action<double>? DownloadProgress;

    public async Task<Version?> CheckForUpdate()
    {
        var (releaseVersion, releaseFound, apiOk) = await CheckReleaseTag();
        if (apiOk)
            return releaseFound && releaseVersion > CurrentVersion ? releaseVersion : null;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.Add("User-Agent", "WindowsStorageCleaner");
            var json = await http.GetStringAsync(VersionUrl);
            var remote = ParseVersionJson(json);
            return remote != null && remote > CurrentVersion ? remote : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<(Version? version, bool found, bool apiOk)> CheckReleaseTag()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.Add("User-Agent", "WindowsStorageCleaner");
            http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            var releases = await http.GetFromJsonAsync<GitHubRelease[]>(ReleasesApiUrl);
            if (releases == null) return (null, false, true);

            foreach (var r in releases)
            {
                if (r.Draft || r.Prerelease) continue;
                var tag = r.TagName?.TrimStart('v');
                if (Version.TryParse(tag, out var v))
                    return (v, true, true);
            }
            return (null, false, true);
        }
        catch
        {
            return (null, false, false);
        }
    }

    public async Task<bool> DownloadAndInstall(Version newVersion)
    {
        var url = $"https://github.com/{Owner}/{Repo}/releases/download/v{newVersion}/{InstallerName}";
        var tempDir = Path.Combine(Path.GetTempPath(), "WindowsStorageCleanerUpdate");
        Directory.CreateDirectory(tempDir);
        var installerPath = Path.Combine(tempDir, InstallerName);

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            http.DefaultRequestHeaders.Add("User-Agent", "WindowsStorageCleaner");
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1;
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var file = File.Create(installerPath);
            var buffer = new byte[81920];
            long read = 0;
            int bytes;
            while ((bytes = await stream.ReadAsync(buffer)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, bytes));
                read += bytes;
                if (total > 0)
                    DownloadProgress?.Invoke((double)read / total);
            }
        }
        catch
        {
            return false;
        }

        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "WindowsStorageCleaner.exe";
        var tempBat = Path.Combine(Path.GetTempPath(), "WindowsStorageCleanerUpdate", "restart.bat");
        var batContent = $"@echo off\r\ntimeout /t 5 /nobreak >nul\r\n:wait\r\ntasklist /fi \"imagename eq msiexec.exe\" 2>nul | find /i \"msiexec.exe\" >nul\r\nif not errorlevel 1 (\r\n  timeout /t 3 /nobreak >nul\r\n  goto wait\r\n)\r\nstart \"\" \"{exePath}\"\r\ndel \"%~f0\"\r\n";
        File.WriteAllText(tempBat, batContent);

        var psi = new ProcessStartInfo
        {
            FileName = "msiexec",
            Arguments = $"/i \"{installerPath}\" /qn",
            UseShellExecute = true
        };
        Process.Start(psi);
        Process.Start(new ProcessStartInfo
        {
            FileName = tempBat,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
        return true;
    }

    private static Version? ParseVersionJson(string json)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var vStr = doc.RootElement.GetProperty("version").GetString();
            if (vStr != null && Version.TryParse(vStr, out var v))
                return v;
        }
        catch { }
        return null;
    }

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }
        [JsonPropertyName("draft")]
        public bool Draft { get; set; }
        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
    }
}
