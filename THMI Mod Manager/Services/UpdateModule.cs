using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager
{
    public class UpdateCheckResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("currentVersion")]
        public string CurrentVersion { get; set; } = string.Empty;

        [JsonPropertyName("latestVersion")]
        public string LatestVersion { get; set; } = string.Empty;

        [JsonPropertyName("isUpdateAvailable")]
        public bool IsUpdateAvailable { get; set; }

        [JsonPropertyName("releaseNotes")]
        public string ReleaseNotes { get; set; } = string.Empty;

        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("publishedAt")]
        public DateTime? PublishedAt { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("fileSize")]
        public long FileSize { get; set; }

        [JsonPropertyName("assetId")]
        public int AssetId { get; set; }
    }

    public class GitHubRelease
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("assets_url")]
        public string AssetsUrl { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("node_id")]
        public string NodeId { get; set; } = string.Empty;

        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("target_commitish")]
        public string TargetCommitish { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = new List<GitHubAsset>();
    }

    public class GitHubAsset
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("node_id")]
        public string NodeId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("uploader")]
        public GitHubUploader? Uploader { get; set; }

        [JsonPropertyName("content_type")]
        public string ContentType { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("download_count")]
        public int DownloadCount { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }

    public class GitHubUploader
    {
        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; } = string.Empty;

        [JsonPropertyName("gravatar_id")]
        public string GravatarId { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("followers_url")]
        public string FollowersUrl { get; set; } = string.Empty;

        [JsonPropertyName("following_url")]
        public string FollowingUrl { get; set; } = string.Empty;

        [JsonPropertyName("gists_url")]
        public string GistsUrl { get; set; } = string.Empty;

        [JsonPropertyName("starred_url")]
        public string StarredUrl { get; set; } = string.Empty;

        [JsonPropertyName("subscriptions_url")]
        public string SubscriptionsUrl { get; set; } = string.Empty;

        [JsonPropertyName("organizations_url")]
        public string OrganizationsUrl { get; set; } = string.Empty;

        [JsonPropertyName("repos_url")]
        public string ReposUrl { get; set; } = string.Empty;

        [JsonPropertyName("events_url")]
        public string EventsUrl { get; set; } = string.Empty;

        [JsonPropertyName("received_events_url")]
        public string ReceivedEventsUrl { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("site_admin")]
        public bool SiteAdmin { get; set; }
    }

    public class UpdateDownloadResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("tempPath")]
        public string? TempPath { get; set; }

        [JsonPropertyName("downloadedBytes")]
        public long DownloadedBytes { get; set; }

        [JsonPropertyName("totalBytes")]
        public long TotalBytes { get; set; }
    }

    public class UpdateApplyResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("restartRequired")]
        public bool RestartRequired { get; set; }

        [JsonPropertyName("updaterPath")]
        public string? UpdaterPath { get; set; }
    }

    public class Architecture
    {
        public static string Current => RuntimeInformation.OSArchitecture.ToString().Contains("Arm") ? "ARM64" : 
                                         (Environment.Is64BitOperatingSystem ? "x64" : "x86");

        public static string PackageSuffix
        {
            get
            {
                var os = RuntimeInformation.OSDescription.ToLowerInvariant();
                var arch = Environment.Is64BitOperatingSystem ? "x64" : "x86";

                if (os.Contains("windows"))
                {
                    return Environment.Is64BitOperatingSystem ? "win-x64" : "win-x86";
                }
                else if (os.Contains("linux"))
                {
                    if (RuntimeInformation.OSArchitecture.ToString().Contains("Arm"))
                    {
                        return RuntimeInformation.OSArchitecture.ToString().Contains("64") ? "linux-arm64" : "linux-arm";
                    }
                    return "linux-x64";
                }
                else if (os.Contains("darwin") || os.Contains("macos") || os.Contains("osx"))
                {
                    return RuntimeInformation.OSArchitecture.ToString().Contains("Arm") ? "osx-arm64" : "osx-x64";
                }

                return Environment.Is64BitOperatingSystem ? "win-x64" : "win-x86";
            }
        }
    }

    public class UpdateModule
    {
        private readonly HttpClient _httpClient;
        private readonly AppConfigManager _appConfig;
        private const string GitHubApiUrl = "https://api.github.com/repos/GlassesMita/THMI-Mod-Manager/releases/latest";
        private const string Owner = "GlassesMita";
        private const string Repo = "THMI-Mod-Manager";

        public UpdateModule(HttpClient httpClient, AppConfigManager appConfig)
        {
            _httpClient = httpClient;
            _appConfig = appConfig;
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(string currentVersion)
        {
            try
            {
                Logger.LogInfo("Checking for updates...");
                
                using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
                request.Headers.UserAgent.ParseAdd("THMI-Mod-Manager");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning($"GitHub API returned status code: {(int)response.StatusCode}");
                    return new UpdateCheckResult
                    {
                        Success = false,
                        Message = $"Failed to check for updates: HTTP {(int)response.StatusCode}"
                    };
                }

                var content = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GitHubRelease>(content);

                if (release == null)
                {
                    return new UpdateCheckResult
                    {
                        Success = false,
                        Message = "Failed to parse release information"
                    };
                }

                var latestVersion = release.TagName.TrimStart('v');
                var currentSemVer = ParseVersion(currentVersion);
                var latestSemVer = ParseVersion(latestVersion);

                string? downloadUrl = null;
                long fileSize = 0;
                int assetId = 0;
                string packageSuffix = Architecture.PackageSuffix;

                foreach (var asset in release.Assets)
                {
                    var assetName = asset.Name.ToLower();
                    if (assetName.Contains(packageSuffix) && (assetName.EndsWith(".zip") || assetName.EndsWith(".7z")))
                    {
                        downloadUrl = asset.BrowserDownloadUrl;
                        fileSize = asset.Size;
                        assetId = asset.Id;
                        break;
                    }
                }

                if (downloadUrl == null)
                {
                    Logger.LogWarning($"No matching {packageSuffix} package found");
                    return new UpdateCheckResult
                    {
                        Success = false,
                        Message = $"No update package found for {packageSuffix} platform"
                    };
                }

                var isUpdateAvailable = latestSemVer != null && currentSemVer != null && latestSemVer.IsNewerThan(currentSemVer);

                Logger.LogInfo($"Current version: {currentVersion}, Latest version: {latestVersion}, Update available: {isUpdateAvailable}");

                return new UpdateCheckResult
                {
                    Success = true,
                    CurrentVersion = currentVersion,
                    LatestVersion = latestVersion,
                    IsUpdateAvailable = isUpdateAvailable,
                    ReleaseNotes = release.Body ?? string.Empty,
                    DownloadUrl = downloadUrl ?? string.Empty,
                    PublishedAt = release.PublishedAt,
                    FileSize = fileSize,
                    AssetId = assetId
                };
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error checking for updates");
                
                return new UpdateCheckResult
                {
                    Success = false,
                    Message = $"Failed to check for updates: {ex.Message}"
                };
            }
        }

        public async Task<object> CheckForUpdatesApiAsync(string currentVersion)
        {
            var result = await CheckForUpdatesAsync(currentVersion);
            var now = DateTime.Now;
            
            if (result.Success)
            {
                _appConfig.Set("[Updates]LastCheckTime", now.ToString("o"));
                
                return new
                {
                    success = true,
                    isUpdateAvailable = result.IsUpdateAvailable,
                    currentVersion = result.CurrentVersion,
                    latestVersion = result.LatestVersion,
                    releaseNotes = result.ReleaseNotes,
                    downloadUrl = result.DownloadUrl,
                    publishedAt = result.PublishedAt,
                    message = result.IsUpdateAvailable 
                        ? $"New version {result.LatestVersion} is available" 
                        : "No updates available"
                };
            }
            else
            {
                return new
                {
                    success = false,
                    message = result.Message
                };
            }
        }

        public async Task<UpdateDownloadResult> DownloadUpdateAsync(string downloadUrl)
        {
            try
            {
                Logger.LogInfo($"Starting download update: {downloadUrl}");

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var tempPath = Path.Combine(Path.GetTempPath(), $"THMI_Update_{timestamp}_{uniqueId}");
                Directory.CreateDirectory(tempPath);

                var fileName = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);
                var zipPath = Path.Combine(tempPath, fileName);

                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    return new UpdateDownloadResult
                    {
                        Success = false,
                        Message = $"Download failed: HTTP {(int)response.StatusCode}"
                    };
                }

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                Logger.LogInfo($"File size: {totalBytes:N0} bytes");

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 131072);
                var buffer = new byte[131072];
                long downloadedBytes = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progress = (double)downloadedBytes / totalBytes * 100;
                        Logger.LogDebug($"Download progress: {progress:F1}% ({downloadedBytes:N0}/{totalBytes:N0})");
                    }
                }

                Logger.LogInfo($"Download complete: {zipPath}");
                return new UpdateDownloadResult
                {
                    Success = true,
                    Message = "Download successful",
                    TempPath = tempPath,
                    DownloadedBytes = downloadedBytes,
                    TotalBytes = totalBytes
                };
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error downloading update");
                return new UpdateDownloadResult
                {
                    Success = false,
                    Message = $"Download failed: {ex.Message}"
                };
            }
        }

        public UpdateApplyResult PrepareUpdate(string tempPath, string currentExePath, string newVersion = "")
        {
            try
            {
                Logger.LogInfo($"Preparing update, temp directory: {tempPath}, new version: {newVersion}");

                var exeDir = Path.GetDirectoryName(currentExePath) ?? Directory.GetCurrentDirectory();
                var exeName = Path.GetFileName(currentExePath);

                var updateScriptPath = Path.Combine(tempPath, "update.bat");
                var updaterExePath = Path.Combine(tempPath, "THMI.Mod.Manager.Updater.exe");

                if (!Directory.Exists(tempPath))
                {
                    return new UpdateApplyResult
                    {
                        Success = false,
                        Message = "Update package directory does not exist"
                    };
                }

                var zipFiles = Directory.GetFiles(tempPath, "*.zip");
                if (zipFiles.Length == 0)
                {
                    return new UpdateApplyResult
                    {
                        Success = false,
                        Message = "No update package found"
                    };
                }

                var zipPath = zipFiles[0];
                Logger.LogInfo($"Extracting update package: {zipPath}");

                var extractPath = Path.Combine(tempPath, "extracted");
                Directory.CreateDirectory(extractPath);

                ZipFile.ExtractToDirectory(zipPath, extractPath);

                var updateBatContent = $@"@echo off
echo Updating THMI Mod Manager...
timeout /t 2 /nobreak >nul

echo Waiting for program to exit...
timeout /t 3 /nobreak >nul

echo Copying files...
xcopy /E /Y ""{extractPath}\*"" ""{exeDir}"" >nul 2>&1

echo Cleaning up temporary files...
rmdir /s /q ""{tempPath}"" >nul 2>&1

echo Update complete! Starting program...
start "" ""{Path.Combine(exeDir, exeName)}"" --updated-version=""{newVersion}""

exit
";

                File.WriteAllText(updateScriptPath, updateBatContent);

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{updateScriptPath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = tempPath
                };

                Logger.LogInfo($"Created update script: {updateScriptPath}");
                return new UpdateApplyResult
                {
                    Success = true,
                    Message = "Update ready",
                    RestartRequired = true,
                    UpdaterPath = updateScriptPath
                };
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error preparing update");
                return new UpdateApplyResult
                {
                    Success = false,
                    Message = $"Failed to prepare update: {ex.Message}",
                    RestartRequired = false
                };
            }
        }

        public async Task ApplyUpdateAsync(string zipPath, string appDirectory)
        {
            try
            {
                Logger.LogInfo($"Applying update: {zipPath} -> {appDirectory}");

                if (!File.Exists(zipPath))
                {
                    throw new FileNotFoundException("Update package not found", zipPath);
                }

                var tempExtractPath = Path.Combine(Path.GetTempPath(), $"THMI_Extract_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempExtractPath);

                Logger.LogInfo($"Extracting to temporary directory: {tempExtractPath}");
                ZipFile.ExtractToDirectory(zipPath, tempExtractPath);

                var files = Directory.GetFiles(tempExtractPath, "*", SearchOption.AllDirectories);
                var dirs = Directory.GetDirectories(tempExtractPath, "*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var relativePath = file.Substring(tempExtractPath.Length + 1);
                    var destPath = Path.Combine(appDirectory, relativePath);

                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    try
                    {
                        File.Copy(file, destPath, true);
                        Logger.LogDebug($"Copied file: {relativePath}");
                    }
                    catch (IOException ex)
                    {
                        Logger.LogWarning($"Failed to copy file {relativePath}: {ex.Message}");
                    }
                }

                try
                {
                    Directory.Delete(tempExtractPath, true);
                    Logger.LogInfo("Cleanup temporary directory complete");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to cleanup temporary directory: {ex.Message}");
                }

                Logger.LogInfo("Update application complete");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error applying update");
                throw;
            }
        }

        public async Task<UpdateDownloadResult> DownloadAndApplyUpdateAsync(string downloadUrl, string appDirectory, IProgress<double>? progress = null)
        {
            var downloadResult = await DownloadUpdateAsync(downloadUrl);
            if (!downloadResult.Success)
            {
                return downloadResult;
            }

            return new UpdateDownloadResult
            {
                Success = true,
                Message = "Download complete, ready to restart app",
                TempPath = downloadResult.TempPath,
                DownloadedBytes = downloadResult.DownloadedBytes,
                TotalBytes = downloadResult.TotalBytes
            };
        }

        private SemanticVersion? ParseVersion(string version)
        {
            try
            {
                return SemanticVersion.Parse(version);
            }
            catch
            {
                Logger.LogWarning($"Failed to parse version: {version}");
                return null;
            }
        }

        public static string GetExecutablePath()
        {
            var process = Process.GetCurrentProcess();
            var mainModule = process.MainModule;
            if (mainModule != null && !string.IsNullOrEmpty(mainModule.FileName))
            {
                return mainModule.FileName;
            }

            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                return exePath;
            }

            exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                return exePath;
            }

            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                return assemblyLocation;
            }

            return AppContext.BaseDirectory;
        }
    }
}
