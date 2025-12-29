using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace THMI_Mod_Manager.Services
{
    /// <summary>
    /// Service for checking updates from GitHub releases
    /// </summary>
    public class UpdateCheckService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UpdateCheckService> _logger;
        private readonly AppConfigManager _appConfig;

        private const string GITHUB_API_BASE = "https://api.github.com";
        private const string GITHUB_RELEASES_ENDPOINT = "/repos/{0}/releases/latest";

        public UpdateCheckService(HttpClient httpClient, ILogger<UpdateCheckService> logger, AppConfigManager appConfig)
        {
            _httpClient = httpClient;
            _logger = logger;
            _appConfig = appConfig;
            
            // Set up HTTP client with proper headers for GitHub API
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "THMI-Mod-Manager/1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        /// <summary>
        /// Checks for updates from GitHub releases
        /// </summary>
        /// <param name="currentVersion">Current version of the application</param>
        /// <param name="githubRepo">GitHub repository in format "owner/repo"</param>
        /// <returns>Update check result</returns>
        public async Task<UpdateCheckResult> CheckForUpdatesAsync(string currentVersion, string githubRepo)
        {
            try
            {
                _logger.LogInformation($"Checking for updates. Current version: {currentVersion}, Repository: {githubRepo}");

                // Parse current version
                if (!SemanticVersion.TryParse(currentVersion, out var currentSemVer))
                {
                    _logger.LogError($"Failed to parse current version: {currentVersion}");
                    return new UpdateCheckResult
                    {
                        Success = false,
                        ErrorMessage = $"Invalid current version format: {currentVersion}"
                    };
                }

                // Get latest release from GitHub
                var latestRelease = await GetLatestGitHubReleaseAsync(githubRepo);
                if (latestRelease == null)
                {
                    return new UpdateCheckResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to retrieve latest release information"
                    };
                }

                // Parse latest version
                var latestVersionTag = latestRelease.TagName.TrimStart('v'); // Remove 'v' prefix if present
                if (!SemanticVersion.TryParse(latestVersionTag, out var latestSemVer))
                {
                    _logger.LogError($"Failed to parse latest version: {latestVersionTag}");
                    return new UpdateCheckResult
                    {
                        Success = false,
                        ErrorMessage = $"Invalid latest version format: {latestVersionTag}"
                    };
                }

                // Compare versions
                bool isUpdateAvailable = latestSemVer.IsNewerThan(currentSemVer);
                
                var result = new UpdateCheckResult
                {
                    Success = true,
                    CurrentVersion = currentSemVer.ToString(),
                    LatestVersion = latestSemVer.ToString(),
                    IsUpdateAvailable = isUpdateAvailable,
                    ReleaseNotes = latestRelease.Body,
                    DownloadUrl = latestRelease.HtmlUrl,
                    PublishedAt = latestRelease.PublishedAt
                };

                if (isUpdateAvailable)
                {
                    _logger.LogInformation($"Update available: {currentSemVer} → {latestSemVer}");
                }
                else
                {
                    _logger.LogInformation($"No update available. Current version {currentSemVer} is up to date.");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during update check");
                return new UpdateCheckResult
                {
                    Success = false,
                    ErrorMessage = $"Update check failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gets the latest release information from GitHub
        /// </summary>
        private async Task<GitHubRelease?> GetLatestGitHubReleaseAsync(string githubRepo)
        {
            try
            {
                var endpoint = string.Format(GITHUB_RELEASES_ENDPOINT, githubRepo);
                var url = GITHUB_API_BASE + endpoint;

                _logger.LogInformation($"Fetching latest release from: {url}");

                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"GitHub API request failed with status: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GitHubRelease>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return release;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching GitHub release information");
                return null;
            }
        }

        /// <summary>
        /// Checks if automatic update checking is enabled
        /// </summary>
        public bool IsAutoUpdateCheckEnabled()
        {
            var autoCheckValue = _appConfig.Get("[Update]AutoCheck", "true");
            return autoCheckValue?.ToLower() != "false";
        }

        /// <summary>
        /// Gets the last update check timestamp
        /// </summary>
        public DateTime? GetLastUpdateCheck()
        {
            var lastCheckStr = _appConfig.Get("[Update]LastCheck");
            if (DateTime.TryParse(lastCheckStr, out var lastCheck))
            {
                return lastCheck;
            }
            return null;
        }

        /// <summary>
        /// Records the current update check timestamp
        /// </summary>
        public void RecordUpdateCheck()
        {
            _appConfig.Set("[Update]LastCheck", DateTime.UtcNow.ToString("O"));
        }

        /// <summary>
        /// Determines if an update check should be performed based on the check interval
        /// </summary>
        public bool ShouldCheckForUpdates()
        {
            if (!IsAutoUpdateCheckEnabled())
                return false;

            var lastCheck = GetLastUpdateCheck();
            if (lastCheck == null)
                return true;

            var checkIntervalHours = 24; // Check once per day by default
            var checkIntervalValue = _appConfig.Get("[Update]CheckIntervalHours");
            if (int.TryParse(checkIntervalValue, out var customInterval))
            {
                checkIntervalHours = customInterval;
            }

            return DateTime.UtcNow - lastCheck.Value >= TimeSpan.FromHours(checkIntervalHours);
        }
    }

    /// <summary>
    /// Result of an update check operation
    /// </summary>
    public class UpdateCheckResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CurrentVersion { get; set; }
        public string? LatestVersion { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public string? ReleaseNotes { get; set; }
        public string? DownloadUrl { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    /// <summary>
    /// GitHub release information
    /// </summary>
    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }

        [JsonPropertyName("draft")]
        public bool IsDraft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool IsPrerelease { get; set; }
    }
}