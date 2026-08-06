using System.IO.Compression;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Tommy;
using THMI_Mod_Manager.Models;

namespace THMI_Mod_Manager.Services
{
    public class ModUpdateService
    {
        private readonly AppConfigManager _appConfig;
        private readonly HttpClient _httpClient;
        private const string ThunderStoreApiUrl = "https://thunderstore.io/c/touhou-mystia-izakaya/p/{packageId}";
        private static readonly ConcurrentDictionary<string, UpdateProgress> _updateProgress = new();
        private static readonly HashSet<string> AllowedDownloadHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "github.com", "api.github.com", "objects.githubusercontent.com", "github-releases.githubusercontent.com", "thunderstore.io"
        };

        public ModUpdateService(AppConfigManager appConfig, HttpClient httpClient)
        {
            _appConfig = appConfig;
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public static UpdateProgress? GetUpdateProgress(string fileName)
        {
            return _updateProgress.TryGetValue(fileName, out var progress) ? progress : null;
        }

        private void SetUpdateProgress(string fileName, long bytesDownloaded, long totalBytes, string status)
        {
            _updateProgress[fileName] = new UpdateProgress
            {
                BytesDownloaded = bytesDownloaded,
                TotalBytes = totalBytes,
                Status = status,
                LastUpdated = DateTime.UtcNow
            };
        }

        public async Task<List<ModInfo>> CheckForModUpdatesAsync(List<ModInfo> mods, CancellationToken cancellationToken = default)
        {
            using var concurrency = new SemaphoreSlim(4);
            var updates = mods.Select(async mod =>
            {
                await concurrency.WaitAsync(cancellationToken);
                try
                {
                    var latestVersion = await GetLatestModVersionAsync(mod, cancellationToken);
                    if (latestVersion != null)
                    {
                        mod.LatestVersion = latestVersion.VersionString;
                        mod.DownloadUrl = latestVersion.DownloadUrl;
                        mod.FileSizeBytes = latestVersion.FileSizeBytes;
                        mod.ReleaseNotes = latestVersion.ReleaseNotes;
                        mod.ReleaseHtmlUrl = latestVersion.ReleaseHtmlUrl;
                        mod.DownloadSha256 = latestVersion.Sha256;
                        mod.HasUpdateAvailable = IsVersionNewer(mod.Version, latestVersion.VersionString);
                    }
                    else
                    {
                        mod.HasUpdateAvailable = false;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, $"Failed to check update for mod {mod.Name}");
                    mod.HasUpdateAvailable = false;
                }
                finally
                {
                    concurrency.Release();
                }

                return mod;
            });

            return (await Task.WhenAll(updates)).ToList();
        }

        public async Task<ModUpdateCheckResult?> GetLatestModVersionAsync(ModInfo mod, CancellationToken cancellationToken = default)
        {
            var updateUrl = mod.UpdateUrl ?? mod.ModLink;
            
            if (string.IsNullOrEmpty(updateUrl))
            {
                Logger.LogInfo($"Mod {mod.Name} has no UpdateUrl or ModLink, skipping update check");
                return null;
            }

            try
            {
                var cleanUrl = CleanUrl(updateUrl);
                Logger.LogInfo($"Mod {mod.Name}: raw URL = '{updateUrl}', clean URL = '{cleanUrl}'");
                
                var uri = new Uri(cleanUrl);
                if (uri.Scheme != Uri.UriSchemeHttps || !AllowedDownloadHosts.Contains(uri.Host))
                    return null;
                
                if (uri.Host.Equals("thunderstore.io", StringComparison.OrdinalIgnoreCase))
                {
                    return await CheckThunderStoreUpdateAsync(cleanUrl, cancellationToken);
                }
                else if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || 
                         uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
                {
                    return await CheckGitHubUpdateAsync(cleanUrl, mod.Name, cancellationToken);
                }
                else
                {
                    Logger.LogInfo($"Unsupported update URL host: {uri.Host}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, $"Error checking update for mod {mod.Name}");
                return null;
            }
        }

        private string CleanUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;
            
            return url.Trim()
                      .Replace("`", "")
                      .Replace(" ", "")
                      .Replace("\n", "")
                      .Replace("\r", "")
                      .Replace("\t", "");
        }

        private bool IsZipFile(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);
                
                // Check if file starts with ZIP signature
                if (fs.Length < 4)
                    return false;
                
                var signature = br.ReadUInt32();
                // ZIP files start with 0x04034B50 (PK.. little-endian)
                return signature == 0x04034B50 || 
                       // Also check for other ZIP signatures: 0x06054B50 (end of central directory) or 0x02014B50 (central directory file header)
                       signature == 0x06054B50 || 
                       signature == 0x02014B50 ||
                       // And PK as ASCII at start
                       signature == 0x504B0304; // "PK" followed by 0x030x04
            }
            catch
            {
                // If we can't read the file, assume it's not a ZIP
                return false;
            }
        }

        private async Task<ModUpdateCheckResult?> CheckThunderStoreUpdateAsync(string updateUrl, CancellationToken cancellationToken)
        {
            try
            {
                var packageId = ExtractThunderStorePackageId(updateUrl);
                if (string.IsNullOrEmpty(packageId))
                {
                    return null;
                }

                var apiUrl = $"https://thunderstore.io/api/experimental/package/{packageId}/";
                var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning($"ThunderStore API returned: {(int)response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<ThunderStoreResponse>(content);
                
                if (data?.Latest != null)
                {
                    var latestVersion = data.Latest.VersionNumber;
                    var downloadUrl = $"https://thunderstore.io/package/download/{packageId}/{latestVersion}/";
                    
                    return new ModUpdateCheckResult
                    {
                        VersionString = latestVersion,
                        DownloadUrl = downloadUrl,
                        FileSizeBytes = data.Latest.FileSizeBytes
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, $"Error checking ThunderStore for mod");
                return null;
            }
        }

        private async Task<ModUpdateCheckResult?> CheckGitHubUpdateAsync(string updateUrl, string? modName, CancellationToken cancellationToken)
        {
            try
            {
                string releaseUrl;
                
                if (updateUrl.StartsWith("https://api.github.com/repos/", StringComparison.OrdinalIgnoreCase))
                {
                    releaseUrl = updateUrl;
                    Logger.LogInfo($"Using API URL directly: {releaseUrl}");
                }
                else
                {
                    var (owner, repo, tag) = ExtractGitHubInfo(updateUrl);
                    if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
                    {
                        Logger.LogWarning($"Failed to extract GitHub info from URL: {updateUrl}");
                        return null;
                    }

                    if (!string.IsNullOrEmpty(tag))
                    {
                        releaseUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}";
                    }
                    else
                    {
                        releaseUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
                    }
                    
                    Logger.LogInfo($"Constructed API URL: {releaseUrl}");
                }
                
                var response = await _httpClient.GetAsync(releaseUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning($"GitHub API returned HTTP {(int)response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GitHubReleaseResponse>(content);
                
                if (release?.Assets != null && release.Assets.Count > 0)
                {
                    var dllAsset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
                    if (dllAsset == null)
                    {
                        dllAsset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                    }
                    
                    if (dllAsset != null)
                    {
                        Logger.LogInfo($"Found release asset: {dllAsset.Name}, tag: {release.TagName}");
                        return new ModUpdateCheckResult
                        {
                            VersionString = release.TagName.TrimStart('v'),
                            DownloadUrl = dllAsset.BrowserDownloadUrl,
                            FileSizeBytes = dllAsset.Size,
                            Sha256 = NormalizeSha256(dllAsset.Digest),
                            ReleaseNotes = release.Body,
                            ReleaseHtmlUrl = release.HtmlUrl
                        };
                    }
                    else
                    {
                        Logger.LogWarning($"No .dll or .zip asset found in release");
                    }
                }
                else
                {
                    Logger.LogWarning($"No assets found in release");
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, $"Error checking GitHub for mod {(modName ?? "unknown")}");
                return null;
            }
        }

        public async Task<bool> UpdateModAsync(ModInfo mod)
        {
            if (string.IsNullOrEmpty(mod.DownloadUrl))
            {
                Logger.LogWarning($"No download URL for mod {mod.Name}");
                return false;
            }

            string? tempPath = null;
            try
            {
                Logger.LogInfo($"Starting update for mod: {mod.Name}");

                var pluginsPath = GetPluginsPath();
                var dllFileName = Path.GetFileNameWithoutExtension(mod.FilePath);
                // Remove .disabled suffix first, then remove .dll extension to get the mod folder name
                var modFolderName = Path.GetFileNameWithoutExtension(dllFileName.Replace(".disabled", "", StringComparison.OrdinalIgnoreCase));
                var modFolder = Path.Combine(pluginsPath, modFolderName);
                var manifestPath = Path.Combine(modFolder, "Manifest.toml");

                if (!Uri.TryCreate(mod.DownloadUrl, UriKind.Absolute, out var downloadUri)
                    || downloadUri.Scheme != Uri.UriSchemeHttps || !AllowedDownloadHosts.Contains(downloadUri.Host))
                {
                    Logger.LogWarning($"Rejected untrusted update URL: {mod.DownloadUrl}");
                    return false;
                }

                tempPath = Path.Combine(Path.GetTempPath(), $"THMI_Mod_Update_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempPath);

                var zipPath = Path.Combine(tempPath, "mod.zip");
                
                SetUpdateProgress(mod.FileName, 0, 0, "准备下载...");
                Logger.LogInfo($"Downloading: {mod.DownloadUrl}");
                
                // Retry mechanism for downloads
                HttpResponseMessage response = null;
                int retryCount = 3;
                
                while (retryCount > 0)
                {
                    try
                    {
                        response = await _httpClient.GetAsync(mod.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                        if (response.IsSuccessStatusCode)
                        {
                            break; // Success, exit retry loop
                        }
                        else
                        {
                            Logger.LogWarning($"Download attempt failed with status: {(int)response.StatusCode}, retries left: {retryCount - 1}");
                            retryCount--;
                            if (retryCount > 0)
                            {
                                await Task.Delay(2000); // Wait 2 seconds before retry
                            }
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        Logger.LogWarning($"Download attempt failed with error: {ex.Message}, retries left: {retryCount - 1}");
                        retryCount--;
                        if (retryCount > 0)
                        {
                            await Task.Delay(2000); // Wait 2 seconds before retry
                        }
                    }
                }
                
                if (response == null || !response.IsSuccessStatusCode)
                {
                    Logger.LogError($"Download failed after retries: HTTP {(int)(response?.StatusCode ?? 0)}");
                    SetUpdateProgress(mod.FileName, 0, 0, "下载失败");
                    return false;
                }

                using (response)
                {
                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                if (totalBytes > ModPackageSafety.MaxDownloadBytes)
                    throw new InvalidDataException($"Update exceeds {ModPackageSafety.MaxDownloadBytes} bytes.");
                var bytesDownloaded = 0L;
                
                SetUpdateProgress(mod.FileName, 0, totalBytes, "下载中...");
                
                try
                {
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                    
                    var buffer = new byte[8192];
                    int bytesRead;
                    
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        bytesDownloaded += bytesRead;
                        if (bytesDownloaded > ModPackageSafety.MaxDownloadBytes)
                            throw new InvalidDataException($"Update exceeds {ModPackageSafety.MaxDownloadBytes} bytes.");
                        
                        // Update progress every 100KB or when complete
                        if (bytesDownloaded % (100 * 1024) == 0 || bytesDownloaded == totalBytes)
                        {
                            SetUpdateProgress(mod.FileName, bytesDownloaded, totalBytes, "下载中...");
                        }
                    }
                }
                catch (Exception streamEx)
                {
                    Logger.LogError(streamEx, "Error during download stream processing");
                    SetUpdateProgress(mod.FileName, 0, 0, "下载流处理失败");
                    return false;
                }

                if (totalBytes > 0 && bytesDownloaded != totalBytes)
                    throw new InvalidDataException("Downloaded update size does not match Content-Length.");
                if (!string.IsNullOrEmpty(mod.DownloadSha256) && !VerifySha256(zipPath, mod.DownloadSha256))
                    throw new InvalidDataException("Downloaded update SHA-256 does not match the published asset digest.");
                
                SetUpdateProgress(mod.FileName, totalBytes, totalBytes, "解压中...");
                Logger.LogInfo($"Extracting to: {tempPath}");
                
                // Determine if the downloaded file is a zip archive or a direct DLL file
                var isZipFile = IsZipFile(zipPath);
                
                if (isZipFile)
                {
                    // File is a zip archive, extract it
                    var extractedPath = Path.Combine(tempPath, "payload");
                    Directory.CreateDirectory(extractedPath);
                    ModPackageSafety.ExtractZipSafely(zipPath, extractedPath);
                    // Look for DLL files in the extracted content
                    var dllFiles = Directory.GetFiles(extractedPath, "*.dll", SearchOption.AllDirectories);
                    if (dllFiles.Length == 0)
                    {
                        Logger.LogError("No DLL files found in update package");
                        CleanupTempPath(tempPath);
                        SetUpdateProgress(mod.FileName, 0, 0, "更新失败：未找到 DLL 文件");
                        return false;
                    }

                    ApplyAtomicReplacements(dllFiles.Select(dllFile => (dllFile, Path.Combine(pluginsPath, Path.GetFileName(dllFile)))).ToList());
                }
                else
                {
                    // File is a direct DLL, copy it directly
                    var dllFileNameOnly = Path.GetFileNameWithoutExtension(mod.FilePath) + ".dll";
                    var destDllPath = Path.Combine(pluginsPath, dllFileNameOnly);
                    
                    ApplyAtomicReplacements(new[] { (zipPath, destDllPath) });
                    Logger.LogInfo($"Updated DLL directly: {dllFileNameOnly}");
                }

                var manifestFiles = Directory.GetFiles(tempPath, "Manifest.*", SearchOption.AllDirectories);
                foreach (var manifestFile in manifestFiles)
                {
                    var manifestFileName = Path.GetFileName(manifestFile);
                    var destManifestPath = Path.Combine(modFolder, manifestFileName);
                    File.Copy(manifestFile, destManifestPath, true);
                    Logger.LogInfo($"Updated manifest: {manifestFileName}");
                }

                if (!string.IsNullOrEmpty(mod.LatestVersion))
                {
                    UpdateManifestVersion(manifestPath, mod.LatestVersion);
                }

                SetUpdateProgress(mod.FileName, 0, 0, "更新完成");
                _updateProgress.TryRemove(mod.FileName, out _);

                Logger.LogInfo($"Successfully updated mod: {mod.Name}");
                return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Failed to update mod {mod.Name}");
                SetUpdateProgress(mod.FileName, 0, 0, "更新失败：" + ex.Message);
                return false;
            }
            finally
            {
                if (tempPath is not null)
                    CleanupTempPath(tempPath);
            }
        }

        private static string? NormalizeSha256(string? digest)
        {
            const string prefix = "sha256:";
            return digest?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true ? digest[prefix.Length..] : null;
        }

        private static bool VerifySha256(string filePath, string expectedHash)
        {
            using var stream = File.OpenRead(filePath);
            return string.Equals(Convert.ToHexString(SHA256.HashData(stream)), expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyAtomicReplacements(IEnumerable<(string Source, string Destination)> replacements)
        {
            var prepared = replacements.ToList();
            var pluginsPath = Path.Combine(AppContext.BaseDirectory, "BepInEx", "plugins");
            if (prepared.Count == 0 || prepared.Any(item => !ModPackageSafety.IsPortableExecutable(item.Source) || !ModPackageSafety.IsWithinDirectory(item.Destination, pluginsPath)))
                throw new InvalidDataException("Update contains an invalid DLL or destination.");

            var rollbackDirectory = Path.Combine(Path.GetTempPath(), $"THMI_Mod_Rollback_{Guid.NewGuid():N}");
            Directory.CreateDirectory(rollbackDirectory);
            var replaced = new List<(string Destination, string? Backup)>();
            try
            {
                foreach (var (source, destination) in prepared)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    var candidate = Path.Combine(rollbackDirectory, Guid.NewGuid().ToString("N") + ".dll");
                    File.Copy(source, candidate, true);
                    if (File.Exists(destination))
                    {
                        var backup = Path.Combine(rollbackDirectory, Guid.NewGuid().ToString("N") + ".bak");
                        File.Replace(candidate, destination, backup, true);
                        replaced.Add((destination, backup));
                    }
                    else
                    {
                        File.Move(candidate, destination);
                        replaced.Add((destination, null));
                    }
                }
            }
            catch
            {
                foreach (var (destination, backup) in replaced.AsEnumerable().Reverse())
                {
                    if (backup is null) File.Delete(destination);
                    else File.Replace(backup, destination, null, true);
                }
                throw;
            }
            finally
            {
                if (Directory.Exists(rollbackDirectory))
                    Directory.Delete(rollbackDirectory, true);
            }
        }

        private void UpdateManifestVersion(string manifestPath, string newVersion)
        {
            try
            {
                if (!File.Exists(manifestPath))
                {
                    Logger.LogWarning($"Manifest not found: {manifestPath}");
                    return;
                }

                var content = File.ReadAllText(manifestPath);
                var lines = content.Split('\n');
                var newLines = new List<string>();
                bool updated = false;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("Version", StringComparison.OrdinalIgnoreCase) && 
                        trimmedLine.Contains("="))
                    {
                        var parts = trimmedLine.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            newLines.Add($"{key} = \"{newVersion}\"");
                            updated = true;
                            continue;
                        }
                    }
                    newLines.Add(line);
                }

                if (updated)
                {
                    File.WriteAllText(manifestPath, string.Join("\n", newLines));
                    Logger.LogInfo($"Updated manifest version to: {newVersion}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, $"Failed to update manifest version");
            }
        }

        private void CleanupTempPath(string tempPath)
        {
            try
            {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, $"Failed to cleanup temp path: {tempPath}");
            }
        }

        private string ExtractThunderStorePackageId(string modLink)
        {
            try
            {
                var uri = new Uri(modLink);
                var pathParts = uri.AbsolutePath.Trim('/').Split('/');
                
                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    if (pathParts[i].Equals("p", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{pathParts[i + 1]}/{pathParts[i + 2]}";
                    }
                }

                var match = Regex.Match(uri.AbsolutePath, @"/p/([a-zA-Z0-9_-]+/[a-zA-Z0-9_-]+)/");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private (string? owner, string? repo, string? tag) ExtractGitHubInfo(string modLink)
        {
            try
            {
                var uri = new Uri(modLink);
                var pathParts = uri.AbsolutePath.Trim('/').Split('/');
                
                if (pathParts.Length >= 3 && pathParts[0].Equals("repos", StringComparison.OrdinalIgnoreCase))
                {
                    var owner = pathParts[1];
                    var repo = pathParts[2].Replace(".git", "");
                    
                    var tag = "";
                    if (pathParts.Length > 4 && pathParts[3].Equals("releases", StringComparison.OrdinalIgnoreCase))
                    {
                        tag = pathParts[5] ?? "";
                    }

                    return (owner, repo, tag);
                }

                return (null, null, null);
            }
            catch
            {
                return (null, null, null);
            }
        }

        private bool IsVersionNewer(string currentVersion, string latestVersion)
        {
            try
            {
                var current = ParseVersion(currentVersion);
                var latest = ParseVersion(latestVersion);
                
                if (current == null || latest == null)
                {
                    return latestVersion != currentVersion;
                }

                return latest > current;
            }
            catch
            {
                return latestVersion != currentVersion;
            }
        }

        private Version? ParseVersion(string versionString)
        {
            try
            {
                var cleanVersion = Regex.Replace(versionString, @"[^0-9.]", "");
                if (Version.TryParse(cleanVersion, out var version))
                {
                    return version;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private string GetPluginsPath()
        {
            var pluginsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "BepInEx", "plugins"));
            Logger.LogInfo($"Using application plugins path: {pluginsPath}");
            return pluginsPath;
        }
    }

    public class ModUpdateCheckResult
    {
        public string VersionString { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public long? FileSizeBytes { get; set; }
        public string? Sha256 { get; set; }
        public string? ReleaseNotes { get; set; }
        public string? ReleaseHtmlUrl { get; set; }
    }

    public class ThunderStoreResponse
    {
        public string? PackageName { get; set; }
        public string? Namespace { get; set; }
        public ThunderStorePackage? Latest { get; set; }
    }

    public class ThunderStorePackage
    {
        public string VersionNumber { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
    }

    public class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("body")]
        public string? Body { get; set; }
        
        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
        
        [JsonPropertyName("assets")]
        public List<GitHubAssetResponse> Assets { get; set; } = new();
    }

    public class GitHubAssetResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("digest")]
        public string? Digest { get; set; }
    }
}
