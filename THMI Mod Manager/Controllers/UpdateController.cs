using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using THMI_Mod_Manager.Services;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;
using System.Net.Http;

namespace THMI_Mod_Manager.Controllers
{
    /// <summary>
    /// Controller for handling update-related operations
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class UpdateController : ControllerBase
    {
        private readonly UpdateCheckService _updateCheckService;
        private readonly UpdateModule _updateModule;
        private readonly AppConfigManager _appConfigManager;
        private readonly IStringLocalizer<UpdateController> _localizer;

        public UpdateController(
            UpdateCheckService updateCheckService,
            UpdateModule updateModule,
            AppConfigManager appConfigManager,
            IStringLocalizer<UpdateController> localizer)
        {
            _updateCheckService = updateCheckService;
            _updateModule = updateModule;
            _appConfigManager = appConfigManager;
            _localizer = localizer;
        }

        /// <summary>
        /// Check for updates for program itself
        /// </summary>
        /// <returns>Program update check result</returns>
        [HttpGet("check-program")]
        public async Task<IActionResult> CheckForProgramUpdates()
        {
            try
            {
                var programName = "THMI Mod Manager";
                var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var currentVersion = assemblyVersion?.ToString(3) ?? "0.0.0";

                var updatesSection = _appConfigManager.GetSection("Updates");
                var autoCheckUpdatesValue = updatesSection.TryGetValue("CheckForUpdates", out var checkUpdates) ? checkUpdates : "true";
                var autoCheckUpdates = autoCheckUpdatesValue?.ToLower() != "false";
                
                if (!autoCheckUpdates)
                {
                    Logger.LogInfo("Update checking is disabled in settings");
                    return Ok(new
                    {
                        success = false,
                        updateCheckingDisabled = true,
                        message = _localizer["UpdateCheckingDisabled"]
                    });
                }

                var updateFrequency = updatesSection.TryGetValue("UpdateFrequency", out var frequency) ? frequency : "startup";
                var lastCheckTimeValue = updatesSection.TryGetValue("LastCheckTime", out var lastCheck) ? lastCheck : string.Empty;
                DateTime? lastCheckTime = null;
                
                if (!string.IsNullOrEmpty(lastCheckTimeValue) && DateTime.TryParse(lastCheckTimeValue, out var parsedTime))
                {
                    lastCheckTime = parsedTime;
                }

                var now = DateTime.Now;
                bool shouldCheck = false;

                switch (updateFrequency?.ToLower())
                {
                    case "startup":
                        shouldCheck = true;
                        break;
                    case "weekly":
                        shouldCheck = now.DayOfWeek == DayOfWeek.Monday && 
                                      (!lastCheckTime.HasValue || lastCheckTime.Value.Date < now.Date);
                        break;
                    case "monthly":
                        shouldCheck = now.Day == 1 && 
                                      (!lastCheckTime.HasValue || lastCheckTime.Value.Month < now.Month || lastCheckTime.Value.Year < now.Year);
                        break;
                    default:
                        shouldCheck = true;
                        break;
                }

                if (!shouldCheck)
                {
                    Logger.LogInfo($"Update check skipped due to frequency setting: {updateFrequency}");
                    return Ok(new
                    {
                        success = true,
                        isUpdateAvailable = false,
                        currentVersion = currentVersion,
                        latestVersion = currentVersion,
                        message = _localizer["NoUpdatesAvailable"]
                    });
                }

                Logger.LogInfo($"Checking for program updates. Current version: {currentVersion}, Frequency: {updateFrequency}");

                var result = await _updateModule.CheckForUpdatesAsync(currentVersion);

                if (result.Success)
                {
                    _appConfigManager.Set("[Updates]LastCheckTime", now.ToString("o"));
                    
                    return Ok(new
                    {
                        success = true,
                        isUpdateAvailable = result.IsUpdateAvailable,
                        currentVersion = result.CurrentVersion,
                        latestVersion = result.LatestVersion,
                        releaseNotes = result.ReleaseNotes,
                        downloadUrl = result.DownloadUrl,
                        publishedAt = result.PublishedAt,
                        message = result.IsUpdateAvailable 
                            ? _localizer["UpdateAvailable", result.LatestVersion ?? ""] 
                            : _localizer["NoUpdatesAvailable"]
                    });
                }
                else
                {
                    return Ok(new
                    {
                        success = false,
                        error = result.Message,
                        message = _localizer["UpdateCheckFailed"]
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error during program update check");
                return StatusCode(500, new
                    {
                        success = false,
                        error = ex.Message,
                        message = _localizer["UpdateCheckError"]
                    });
            }
        }

        /// <summary>
        /// Get current program version information
        /// </summary>
        /// <returns>Current program version info</returns>
        [HttpGet("program-version")]
        public IActionResult GetProgramVersion()
        {
            try
            {
                var appSection = _appConfigManager.GetSection("App");
                var programName = appSection.TryGetValue("Name", out var name) ? name : "THMI Mod Manager";
                var version = appSection.TryGetValue("Version", out var versionValue) ? versionValue : "0.0.1";
                var versionCode = appSection.TryGetValue("VersionCode", out var code) ? code : "1";

                return Ok(new
                    {
                        success = true,
                        programName = programName,
                        version = version,
                        versionCode = versionCode,
                        message = _localizer["CurrentVersionInfo", programName, version]
                    });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error getting program version information");
                return StatusCode(500, new
                    {
                        success = false,
                        error = ex.Message,
                        message = _localizer["VersionInfoError"]
                    });
            }
        }

        /// <summary>
        /// Download update package
        /// </summary>
        [HttpPost("download")]
        public async Task<IActionResult> DownloadUpdate([FromBody] DownloadUpdateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.DownloadUrl))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Download URL cannot be empty"
                    });
                }

                Logger.LogInfo($"Starting download update: {request.DownloadUrl}");

                var result = await _updateModule.DownloadUpdateAsync(request.DownloadUrl);

                if (result.Success)
                {
                    Logger.LogInfo($"Download successful: {result.TempPath}");

                    return Ok(new
                    {
                        success = true,
                        message = "Download successful",
                        tempPath = result.TempPath,
                        downloadedBytes = result.DownloadedBytes,
                        totalBytes = result.TotalBytes
                    });
                }
                else
                {
                    Logger.LogWarning($"Download failed: {result.Message}");
                    return Ok(new
                    {
                        success = false,
                        message = result.Message
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error downloading update");
                return StatusCode(500, new
                    {
                        success = false,
                        error = ex.Message,
                        message = "Download update failed"
                    });
            }
        }

        /// <summary>
        /// Prepare update (download and prepare for installation)
        /// </summary>
        [HttpPost("prepare")]
        public async Task<IActionResult> PrepareUpdate([FromBody] DownloadUpdateRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.DownloadUrl))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Download URL cannot be empty"
                    });
                }

                Logger.LogInfo($"Preparing update: {request.DownloadUrl}");

                var downloadResult = await _updateModule.DownloadUpdateAsync(request.DownloadUrl);
                if (!downloadResult.Success)
                {
                    return Ok(new
                    {
                        success = false,
                        message = downloadResult.Message
                    });
                }

                var exePath = UpdateModule.GetExecutablePath();
                var applyResult = _updateModule.PrepareUpdate(
                    downloadResult.TempPath ?? string.Empty, 
                    exePath,
                    request.NewVersion ?? string.Empty
                );

                if (applyResult.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Update ready",
                        restartRequired = applyResult.RestartRequired,
                        updaterPath = applyResult.UpdaterPath
                    });
                }
                else
                {
                    return Ok(new
                    {
                        success = false,
                        message = applyResult.Message
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error preparing update");
                return StatusCode(500, new
                    {
                        success = false,
                        error = ex.Message,
                        message = "Prepare update failed"
                    });
            }
        }

        /// <summary>
        /// Restart application and apply update
        /// </summary>
        [HttpPost("restart")]
        public IActionResult RestartAndUpdate([FromBody] ApplyUpdateRequest request)
        {
            try
            {
                var exePath = UpdateModule.GetExecutablePath();
                var exeDir = Path.GetDirectoryName(exePath) ?? Directory.GetCurrentDirectory();

                Logger.LogInfo($"Preparing to restart application: {exePath}");

                var vbsContent = "Set WshShell = CreateObject(\"WScript.Shell\")" + Environment.NewLine +
                                  "WshShell.Run \"cmd /c ping -n 3 127.0.0.1 && \"\"" + exePath + "\"\"\", 0, False" + Environment.NewLine +
                                  "WScript.Quit(0)";

                var vbsPath = Path.Combine(Path.GetTempPath(), "THMI_Restart.vbs");
                System.IO.File.WriteAllText(vbsPath, vbsContent);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "wscript.exe",
                    Arguments = "\"" + vbsPath + "\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = exeDir
                };

                Process.Start(startInfo);

                return Ok(new
                    {
                        success = true,
                        message = "Application will restart"
                    });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error restarting application");
                return StatusCode(500, new
                    {
                        success = false,
                        error = ex.Message,
                        message = "Restart application failed"
                    });
            }
        }

        /// <summary>
        /// Get current architecture information
        /// </summary>
        [HttpGet("architecture")]
        public IActionResult GetArchitecture()
        {
            try
            {
                return Ok(new
                    {
                        success = true,
                        architecture = Architecture.Current,
                        packageSuffix = Architecture.PackageSuffix,
                        is64Bit = Environment.Is64BitOperatingSystem,
                        osArchitecture = RuntimeInformation.OSArchitecture.ToString()
                    });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error getting architecture information");
                return StatusCode(500, new
                    {
                        success = false,
                        error = ex.Message
                    });
            }
        }
    }

    /// <summary>
    /// Request model for update settings
    /// </summary>
    public class UpdateSettingsRequest
    {
        public bool CheckForUpdates { get; set; }
        public string? UpdateCheckType { get; set; }
        public int UpdateCheckInterval { get; set; }
        public bool AutoDownloadUpdates { get; set; }
        public string? UpdateSource { get; set; }
    }

    /// <summary>
    /// Download update request model
    /// </summary>
    public class DownloadUpdateRequest
    {
        public string? DownloadUrl { get; set; }
        public string? NewVersion { get; set; }
    }

    /// <summary>
    /// Apply update request model
    /// </summary>
    public class ApplyUpdateRequest
    {
        public string? TempPath { get; set; }
    }
}
