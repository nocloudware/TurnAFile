using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace TurnAFile.Core.Services;

public class UpdateInfo
{
    public string TagName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public bool IsNewerVersion { get; set; }
    public DateTime PublishedDate { get; set; }
}

public class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/nocloudware/TurnAFile/releases/latest";

    private readonly ConfigurationService _configService;

    public UpdateService()
    {
        _configService = new ConfigurationService();
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TurnAFile/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");

            using var responseMessage = await client.GetAsync(GitHubApiUrl);
            if ((int)responseMessage.StatusCode == 404)
            {
                System.Diagnostics.Debug.WriteLine("UPDATE: GitHub repo not found or no releases yet.");
                return null;
            }
            if (!responseMessage.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"UPDATE: HTTP {(int)responseMessage.StatusCode} - {responseMessage.ReasonPhrase}");
                return null;
            }
            var response = await responseMessage.Content.ReadAsStringAsync();
            var release = JsonDocument.Parse(response).RootElement;

            var tagName = release.GetProperty("tag_name").GetString() ?? "0.0.0";
            var version = tagName.TrimStart('v');
            var downloadUrl = release.GetProperty("html_url").GetString() ?? "";
            var releaseNotes = release.GetProperty("body").GetString() ?? "";
            var publishedDate = release.GetProperty("published_at").GetDateTime();

            var currentVersion = System.Reflection.Assembly.GetEntryAssembly()
                ?.GetName().Version?.ToString() ?? "0.0.0";

            return new UpdateInfo
            {
                TagName = tagName,
                Version = version,
                DownloadUrl = downloadUrl,
                ReleaseNotes = releaseNotes,
                IsNewerVersion = IsNewerVersion(version, currentVersion),
                PublishedDate = publishedDate
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error verificando actualizaciones: {ex.Message}");
            return null;
        }
    }

    private bool IsNewerVersion(string v1, string v2)
    {
        try { return Version.Parse(v1) > Version.Parse(v2); }
        catch { return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase) > 0; }
    }

    public bool ShouldCheckToday()
    {
        var s = _configService.LoadSettings();
        return s.LastUpdateCheckDate == null || (DateTime.Now - s.LastUpdateCheckDate.Value).TotalDays >= 1;
    }

    public void MarkCheckedToday()
    {
        var s = _configService.LoadSettings();
        s.LastUpdateCheckDate = DateTime.Now;
        _configService.SaveSettings(s);
    }

    public void IgnoreVersion(string version)
    {
        var s = _configService.LoadSettings();
        s.IgnoredUpdateVersion = version;
        _configService.SaveSettings(s);
    }

    public bool IsVersionIgnored(string version)
    {
        return _configService.LoadSettings().IgnoredUpdateVersion == version;
    }
}