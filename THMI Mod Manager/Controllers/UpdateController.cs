using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using THMI_Mod_Manager.Services;

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
        private readonly AppConfigManager _appConfigManager;
        private readonly ILogger<UpdateController> _logger;
        private readonly IStringLocalizer<UpdateController> _localizer;

        public UpdateController(
            UpdateCheckService updateCheckService,
            AppConfigManager appConfigManager,
            ILogger<UpdateController> logger,
            IStringLocalizer<UpdateController> localizer)
        {
            _updateCheckService = updateCheckService;
            _appConfigManager = appConfigManager;
            _logger = logger;
            _localizer = localizer;
        }

        /// <summary>
        /// Check for updates for the program itself
        /// </summary>
        /// <returns>Program update check result</returns>
        [HttpGet("check-program")]
        public async Task<IActionResult> CheckForProgramUpdates()
        {
            try
            {
                // Check if update checking is enabled
                var updatesSection = _appConfigManager.GetSection("Updates");
                if (!bool.TryParse(updatesSection.TryGetValue("CheckForUpdates", out var checkEnabled) ? checkEnabled : "True", out var isEnabled) || !isEnabled)
                {
                    return Ok(new
                    {
                        success = true,
                        updateCheckingDisabled = true,
                        message = _localizer["UpdateCheckingDisabled"]
                    });
                }

                // Get program information from AppConfig
                var appSection = _appConfigManager.GetSection("App");
                var programName = appSection.TryGetValue("Name", out var name) ? name : "THMI Mod Manager";
                var currentVersion = appSection.TryGetValue("Version", out var version) ? version : "0.0.1";
                
                // Get update source configuration - use program-specific source if available
                var updateSource = updatesSection.TryGetValue("ProgramUpdateSource", out var programSource) ? programSource : "GlassesMita/THMI-Mod-Manager";

                _logger.LogInformation($"Checking for program updates. Current version: {currentVersion}, Source: {updateSource}");

                // Perform update check
                var result = await _updateCheckService.CheckForUpdatesAsync(currentVersion, updateSource);

                if (result.Success)
                {
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
                        error = result.ErrorMessage,
                        message = _localizer["UpdateCheckFailed"]
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during program update check");
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
                _logger.LogError(ex, "Error getting program version information");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = _localizer["VersionInfoError"]
                });
            }
        }

        /// <summary>
        /// Check for updates for the MetaIzakaya mod
        /// </summary>
        /// <returns>Update check result</returns>
        [HttpGet("check")]
        public async Task<IActionResult> CheckForUpdates()
        {
            try
            {
                // Check if update checking is enabled
                var updatesSection = _appConfigManager.GetSection("Updates");
                if (!bool.TryParse(updatesSection.TryGetValue("CheckForUpdates", out var checkEnabled) ? checkEnabled : "True", out var isEnabled) || !isEnabled)
                {
                    return Ok(new
                    {
                        success = true,
                        updateCheckingDisabled = true,
                        message = _localizer["UpdateCheckingDisabled"]
                    });
                }

                // Get update source configuration
                var updateSource = updatesSection.TryGetValue("UpdateSource", out var source) ? source : "MetaMikuAI/MetaIzakaya";
                var currentVersion = "0.7.0";

                _logger.LogInformation($"Checking for updates. Current version: {currentVersion}, Source: {updateSource}");

                // Perform update check
                var result = await _updateCheckService.CheckForUpdatesAsync(currentVersion, updateSource);

                if (result.Success)
                {
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
                        error = result.ErrorMessage,
                        message = _localizer["UpdateCheckFailed"]
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during update check");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = _localizer["UpdateCheckError"]
                });
            }
        }

        /// <summary>
        /// Get current version information
        /// </summary>
        /// <returns>Current version info</returns>
        [HttpGet("version")]
        public IActionResult GetCurrentVersion()
        {
            try
            {
                var version = "0.7.0";
                var modName = "MetaIzakaya";
                var modAuthor = "MetaMikuAI";
                var modDescription = "为居酒屋模组添加自定义菜品、场景交互和顾客行为扩展";
                var modLink = "https://github.com/MetaMikuAI/MetaIzakaya";

                return Ok(new
                {
                    success = true,
                    modName = modName,
                    version = version,
                    author = modAuthor,
                    description = modDescription,
                    modLink = modLink,
                    message = _localizer["CurrentVersionInfo", modName, version]
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting version information");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = _localizer["VersionInfoError"]
                });
            }
        }

        /// <summary>
        /// Update update check settings
        /// </summary>
        /// <param name="settings">Update settings</param>
        /// <returns>Result of the update operation</returns>
        [HttpPost("settings")]
        public IActionResult UpdateSettings([FromBody] UpdateSettingsRequest settings)
        {
            try
            {
                // Update settings using the Set method
                _appConfigManager.Set("[Updates]CheckForUpdates", settings.CheckForUpdates.ToString(), false);
                _appConfigManager.Set("[Updates]UpdateCheckType", settings.UpdateCheckType ?? "GitHub", false);
                _appConfigManager.Set("[Updates]UpdateCheckInterval", settings.UpdateCheckInterval.ToString(), false);
                _appConfigManager.Set("[Updates]AutoDownloadUpdates", settings.AutoDownloadUpdates.ToString(), false);
                _appConfigManager.Set("[Updates]UpdateSource", settings.UpdateSource ?? "MetaMikuAI/MetaIzakaya", false);

                // Save configuration
                _appConfigManager.Save();

                _logger.LogInformation("Update settings saved successfully");

                return Ok(new
                {
                    success = true,
                    message = _localizer["UpdateSettingsSaved"]
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving update settings");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    message = _localizer["UpdateSettingsError"]
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
}