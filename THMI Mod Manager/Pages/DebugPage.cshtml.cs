using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Antiforgery;
using System.Diagnostics;
using THMI_Mod_Manager.Services;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Net.NetworkInformation;
using System.Net.Http;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;

namespace THMI_Mod_Manager.Pages
{
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class DebugModel : PageModel
    {
        private readonly THMI_Mod_Manager.Services.AppConfigManager _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;

        public string AppName { get; set; } = "THMI Mod Manager";
        public string AppVersion { get; set; } = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion 
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) 
            ?? "0.0.0";
        public string RuntimeVersion { get; set; } = "";
        public string OSVersion { get; set; } = "";
        public string OSFriendlyName { get; set; } = "";
        public string OSProductName { get; set; } = "";
        public string BaseDirectory { get; set; } = "";
        public string CurrentTime { get; set; } = "";

        public bool IsDevMode { get; set; } = false;
        public bool ShowCVEWarning { get; set; } = true;
        public bool EnableVerboseLogging { get; set; } = false;
        public bool DisableCache { get; set; } = false;

        public string ConfigFilePath { get; set; } = "";
        public string ConfigLastModified { get; set; } = "";
        public string LogFilePath { get; set; } = "";
        public string LogFileSize { get; set; } = "";

        public DebugModel(THMI_Mod_Manager.Services.AppConfigManager appConfig, IHttpClientFactory httpClientFactory)
        {
            _appConfig = appConfig;
            _httpClientFactory = httpClientFactory;
        }

        public void OnGet()
        {
            Logger.LogInfo("Debug page accessed");

            try
            {
                Logger.LogInfo("Starting to collect system information for debug page");
                AppName = _appConfig.Get("[App]Name", "THMI Mod Manager") ?? "THMI Mod Manager";
                RuntimeVersion = Environment.Version.ToString();
                OSVersion = Environment.OSVersion.ToString();
                OSFriendlyName = GetOSFriendlyName();
                OSProductName = GetOSProductName();
                Logger.LogInfo($"System Information - OS: {OSFriendlyName} ({OSProductName}), Runtime: {RuntimeVersion}");
                BaseDirectory = AppContext.BaseDirectory;
                CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                var devBuildValue = _appConfig.Get("[Dev]IsDevBuild", "false");
                IsDevMode = devBuildValue?.ToLower() == "true";

                var cveWarningValue = _appConfig.Get("[Dev]ShowCVEWarning", "true");
                ShowCVEWarning = cveWarningValue?.ToLower() != "false";

                var verboseLoggingValue = _appConfig.Get("[Debug]VerboseLogging", "false");
                EnableVerboseLogging = verboseLoggingValue?.ToLower() == "true";

                var disableCacheValue = _appConfig.Get("[Debug]DisableCache", "false");
                DisableCache = disableCacheValue?.ToLower() == "true";

                Logger.LogInfo("Reading configuration file information");
                var configPath = Path.Combine(AppContext.BaseDirectory, "AppConfig.Schale");
                ConfigFilePath = configPath;
                
                if (System.IO.File.Exists(configPath))
                {
                    var fileInfo = new FileInfo(configPath);
                    ConfigLastModified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                    Logger.LogInfo($"Configuration file found: {configPath}, Last modified: {ConfigLastModified}");
                }
                var logPath = Path.Combine(AppContext.BaseDirectory, "logs");
                LogFilePath = logPath;
                Logger.LogInfo($"Log directory: {logPath}");
                
                if (Directory.Exists(logPath))
                {
                    var logFiles = Directory.GetFiles(logPath, "*.log");
                    var totalSize = logFiles.Sum(f => new FileInfo(f).Length);
                    LogFileSize = FormatFileSize(totalSize);
                    Logger.LogInfo($"Found {logFiles.Length} log files totaling {LogFileSize}");
                }
                Logger.LogInfo($"Debug page loaded - DevMode: {IsDevMode}, VerboseLogging: {EnableVerboseLogging}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading debug page: {ex.Message}");
            }
        }

        public IActionResult OnPostToggleDevMode([FromBody] ToggleRequest request)
        {
            try
            {
                _appConfig.Set("[Dev]IsDevBuild", request.enabled.ToString().ToLower());
                _appConfig.Reload();
                Logger.LogInfo($"Developer mode toggled: {request.enabled}");
                return new JsonResult(new { success = true, message = $"Developer mode {(request.enabled ? "enabled" : "disabled")}" });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error toggling developer mode: {ex.Message}");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }



        public IActionResult OnPostToggleCVEWarning([FromBody] ToggleRequest request)
          {
              try
              {
                  _appConfig.Set("[Dev]ShowCVEWarning", request.enabled.ToString().ToLower());
                  _appConfig.Reload();
                  Logger.LogInfo($"CVE warning toggled: {request.enabled}");
                  return new JsonResult(new { success = true, message = $"CVE warning {(request.enabled ? "enabled" : "disabled")}" });
              }
              catch (Exception ex)
              {
                  Logger.LogError($"Error toggling CVE warning: {ex.Message}");
                  return new JsonResult(new { success = false, message = ex.Message });
              }
          }

          public IActionResult OnPostToggleVerboseLogging([FromBody] ToggleRequest request)
          {
              try
              {
                  _appConfig.Set("[Debug]VerboseLogging", request.enabled.ToString().ToLower());
                  _appConfig.Reload();
                  Logger.LogInfo($"Verbose logging toggled: {request.enabled}");
                  return new JsonResult(new { success = true, message = $"Verbose logging {(request.enabled ? "enabled" : "disabled")}" });
              }
              catch (Exception ex)
              {
                  Logger.LogError($"Error toggling verbose logging: {ex.Message}");
                  return new JsonResult(new { success = false, message = ex.Message });
              }
          }

        public IActionResult OnPostToggleDisableCache([FromBody] ToggleRequest request)
        {
            try
            {
                _appConfig.Set("[Debug]DisableCache", request.enabled.ToString().ToLower());
                _appConfig.Reload();
                Logger.LogInfo($"Cache toggled: {request.enabled}");
                return new JsonResult(new { success = true, message = $"Cache {(request.enabled ? "disabled" : "enabled")}" });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error toggling cache: {ex.Message}");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public IActionResult OnPostReloadConfig()
        {
            try
            {
                _appConfig.Reload();
                Logger.LogInfo("Configuration reloaded via debug panel");
                return new JsonResult(new { success = true, message = "Configuration reloaded successfully" });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error reloading configuration: {ex.Message}");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public IActionResult OnGetViewConfig()
        {
            try
            {
                Logger.LogInfo("User requested configuration file view");
                var configPath = Path.Combine(AppContext.BaseDirectory, "AppConfig.Schale");
                
                if (!System.IO.File.Exists(configPath))
                {
                    Logger.LogWarning("Configuration file not found when requested");
                return new JsonResult(new { success = false, message = "Config file not found" });
                }

                var configContent = System.IO.File.ReadAllText(configPath);
                var configLines = configContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                var configData = new Dictionary<string, string>();
                foreach (var line in configLines)
                {
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith("["))
                    {
                        var parts = trimmedLine.Split('=');
                        if (parts.Length == 2)
                        {
                            configData[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }

                return new JsonResult(new { success = true, data = configData });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error viewing configuration: {ex.Message}");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public IActionResult OnGetViewConfigRaw()
        {
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "AppConfig.Schale");
                
                if (!System.IO.File.Exists(configPath))
                {
                    return Content("Cannot display: Configuration file not found", "text/plain");
                }

                var configContent = System.IO.File.ReadAllText(configPath);
                Logger.LogInfo("Successfully returned configuration file content");
                return Content(configContent, "text/plain");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error viewing raw configuration: {ex.Message}");
                return Content("Cannot display: " + ex.Message, "text/plain");
            }
        }



        public IActionResult OnGetViewLogs()
        {
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "logs");
                
                if (!Directory.Exists(logPath))
                {
                    return new JsonResult(new { success = false, message = "Logs directory not found", logs = "" });
                }

                var logFiles = Directory.GetFiles(logPath, "*.log")
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .Take(5)
                    .ToList();

                var allLogs = new List<string>();
                
                foreach (var logFile in logFiles)
                {
                    allLogs.Add($"=== {Path.GetFileName(logFile)} ===");
                    var lines = System.IO.File.ReadLines(logFile).Take(100);
                    allLogs.AddRange(lines);
                    allLogs.Add("");
                }

                return new JsonResult(new { success = true, logs = string.Join("\n", allLogs) });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error viewing logs: {ex.Message}");
                return new JsonResult(new { success = false, message = ex.Message, logs = "" });
            }
        }

        public IActionResult OnPostDownloadLogs()
        {
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "logs");
                
                if (!Directory.Exists(logPath))
                {
                    return new JsonResult(new { success = false, message = "Logs directory not found" });
                }

                var logFiles = Directory.GetFiles(logPath, "*.log");
                var allLogs = new List<string>();
                
                foreach (var logFile in logFiles)
                {
                    allLogs.Add($"=== {Path.GetFileName(logFile)} ===");
                    allLogs.Add(System.IO.File.ReadAllText(logFile));
                    allLogs.Add("");
                }

                var logContent = string.Join("\n", allLogs);
                var bytes = System.Text.Encoding.UTF8.GetBytes(logContent);
                
                var productName = _appConfig.Get("[App]Name", "THMI Mod Manager");
                var version = _appConfig.Get("[App]Version", "0.0.1");
                var fileName = $"{productName}_{version}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                
                return File(bytes, "text/plain", fileName);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error downloading logs: {ex.Message}");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public IActionResult OnPostClearLogs()
        {
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "logs");
                
                if (Directory.Exists(logPath))
                {
                    var logFiles = Directory.GetFiles(logPath, "*.log");
                    foreach (var logFile in logFiles)
                    {
                        System.IO.File.Delete(logFile);
                    }
                    Logger.LogInfo($"Cleared {logFiles.Length} log files");
                }

                return new JsonResult(new { success = true, message = "Logs cleared successfully" });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error clearing logs: {ex.Message}");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public IActionResult OnPostResetAllSettings()
        {
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "AppConfig.Schale");
                
                if (System.IO.File.Exists(configPath))
                {
                    var backupPath = configPath + ".backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    System.IO.File.Copy(configPath, backupPath);
                    Logger.LogInfo($"Settings backed up to: {backupPath}");
                }

                _appConfig.Set("[Dev]IsDevBuild", "false");
                _appConfig.Set("[Dev]ShowCVEWarning", "true");
                _appConfig.Set("[Debug]VerboseLogging", "false");
                _appConfig.Set("[Debug]DisableCache", "false");
                _appConfig.Set("[Cursor]CursorType", "default");
                _appConfig.Set("[App]ThemeColor", "#c670ff");
                _appConfig.Set("[Game]LaunchMode", "steam_launch");
                _appConfig.Set("[Game]ModifyTitle", "true");
                _appConfig.Set("[Updates]CheckForUpdates", "true");

                _appConfig.Reload();
                Logger.LogInfo("All settings reset to default");
                
                return new JsonResult(new { success = true, message = "All settings reset to default" });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error resetting settings: {ex.Message}");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnGetCheckNetworkStatus()
        {
            try
            {
                var networkInfo = new NetworkStatusInfo();

                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                var activeInterfaces = networkInterfaces.Where(ni => ni.OperationalStatus == OperationalStatus.Up).ToList();
                networkInfo.IsConnected = activeInterfaces.Any();

                networkInfo.ActiveInterfaces = activeInterfaces.Count();
                networkInfo.TotalInterfaces = networkInterfaces.Count();

                var proxySettings = GetProxySettings();
                networkInfo.IsUsingProxy = proxySettings.IsUsingProxy;
                networkInfo.ProxyServer = proxySettings.ProxyServer;

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var testUrls = new[]
                {
                    new { Name = "Google", Url = "https://www.google.com" },
                    new { Name = "GitHub", Url = "https://github.com" },
                    new { Name = "Steam", Url = "https://store.steampowered.com" }
                };

                var pingResults = new List<PingResult>();
                foreach (var testUrl in testUrls)
                {
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();
                        var response = await httpClient.GetAsync(testUrl.Url);
                        stopwatch.Stop();

                        pingResults.Add(new PingResult
                        {
                            Name = testUrl.Name,
                            Url = testUrl.Url,
                            IsSuccess = response.IsSuccessStatusCode,
                            ResponseTime = stopwatch.ElapsedMilliseconds,
                            StatusCode = (int)response.StatusCode
                        });
                    }
                    catch (Exception ex)
                    {
                        pingResults.Add(new PingResult
                        {
                            Name = testUrl.Name,
                            Url = testUrl.Url,
                            IsSuccess = false,
                            ResponseTime = -1,
                            StatusCode = 0,
                            ErrorMessage = ex.Message
                        });
                    }
                }

                networkInfo.PingResults = pingResults;

                Logger.LogInfo($"Network status checked - Connected: {networkInfo.IsConnected}, Proxy: {networkInfo.IsUsingProxy}");
                return new JsonResult(new { success = true, data = networkInfo });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking network status: {ex.Message}");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        private (bool IsUsingProxy, string ProxyServer) GetProxySettings()
        {
            try
            {
                var proxy = WebRequest.GetSystemWebProxy();
                var proxyUri = proxy.GetProxy(new Uri("https://www.google.com"));
                
                if (proxyUri == null || proxyUri.IsFile || proxyUri.Scheme == "direct")
                {
                    return (false, "");
                }

                return (true, proxyUri.ToString());
            }
            catch
            {
                return (false, "");
            }
        }

        public class NetworkStatusInfo
        {
            public bool IsConnected { get; set; }
            public int ActiveInterfaces { get; set; }
            public int TotalInterfaces { get; set; }
            public bool IsUsingProxy { get; set; }
            public string ProxyServer { get; set; } = "";
            public List<PingResult> PingResults { get; set; } = new List<PingResult>();
        }

        public class PingResult
        {
            public string Name { get; set; } = "";
            public string Url { get; set; } = "";
            public bool IsSuccess { get; set; }
            public long ResponseTime { get; set; }
            public int StatusCode { get; set; }
            public string ErrorMessage { get; set; } = "";
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private string GetOSFriendlyName()
        {
            var os = Environment.OSVersion;
            var platform = os.Platform;
            var version = os.Version;
            var versionString = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";

            string friendlyName = platform switch
            {
                PlatformID.Win32NT => versionString,
                PlatformID.Win32Windows => $"Windows {versionString}",
                PlatformID.Win32S => "Win32s",
                PlatformID.WinCE => "Windows CE",
                PlatformID.Unix => "Unix",
                PlatformID.MacOSX => "macOS",
                _ => "Unknown"
            };

            return friendlyName;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1168:属性名称应与字段名称不同", Justification = "<挂起>")]
        private string GetOSProductName()
        {
            try
            {
                var osVersion = Environment.OSVersion;
                var version = osVersion.Version;
                var platform = osVersion.Platform;

                if (platform == PlatformID.Win32NT)
                {
                    string osName = "Windows";
                    
                    if (version.Build >= 22000)
                    {
                        osName = "Windows 11";
                    }
                    else if (version.Major == 10)
                    {
                        osName = "Windows 10";
                    }
                    else if (version.Major == 6)
                    {
                        if (version.Minor == 3)
                            osName = "Windows 8.1";
                        else if (version.Minor == 2)
                            osName = "Windows 8";
                        else if (version.Minor == 1)
                            osName = "Windows 7";
                        else if (version.Minor == 0)
                            osName = "Windows Vista";
                    }
                    else if (version.Major == 5)
                    {
                        if (version.Minor == 1)
                            osName = "Windows XP";
                        else if (version.Minor == 0)
                            osName = "Windows 2000";
                    }

                    osName = osName.Replace("Enterprise", "Pro");
                    
                    return osName;
                }
                else if (platform == PlatformID.Unix)
                {
                    return RuntimeInformation.OSDescription;
                }
                else if (platform == PlatformID.MacOSX)
                {
                    return "macOS";
                }

                return platform.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        public class ToggleRequest
        {
            public bool enabled { get; set; }
        }
        
        /// <summary>
        /// Triggers a simulated kernel panic for testing purposes
        /// </summary>
        /// <returns>JSON result indicating success or failure</returns>
        public IActionResult OnPostTriggerKernelPanic()
        {
            try
            {
                Logger.LogWarning("Kernel Panic triggered manually via debug panel");
                
                var customException = new Exception("Simulated Kernel Panic - This is a test exception triggered by the user via the debug panel");
                
                // 使用 LogKernelPanic 代替 DisplayKernelPanic，避免在 HTTP 请求处理中显示 UI
                GlobalExceptionHandler.LogKernelPanic(customException);
                
                return new JsonResult(new { success = true, message = "Kernel Panic logged successfully. Check console output." });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error triggering kernel panic: {ex.Message}");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }
}
