using Microsoft.AspNetCore.Mvc;
using THMI_Mod_Manager.Services;
using THMI_Mod_Manager.Models;

namespace THMI_Mod_Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ModsController : ControllerBase
    {
        private readonly ILogger<ModsController> _logger;
        private readonly ModService _modService;
        private readonly ModUpdateService _modUpdateService;
        private readonly AppConfigManager _appConfig;
        private readonly SessionTimeService _sessionTimeService;

        public ModsController(ILogger<ModsController> logger, ModService modService, ModUpdateService modUpdateService, AppConfigManager appConfig, SessionTimeService sessionTimeService)
        {
            _logger = logger;
            _modService = modService;
            _modUpdateService = modUpdateService;
            _appConfig = appConfig;
            _sessionTimeService = sessionTimeService;
        }

        [HttpGet]
        public IActionResult GetMods()
        {
            try
            {
                var mods = _modService.LoadMods();
                _logger.LogInformation($"Retrieved {mods.Count} mods");
                return Ok(new { success = true, mods });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving mods");
                return StatusCode(500, new { success = false, message = $"Failed to retrieve mods: {ex.Message}" });
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetMod(string id)
        {
            try
            {
                var mods = _modService.LoadMods();
                var mod = mods.FirstOrDefault(m => m.UniqueId == id || m.FileName == id);

                if (mod == null)
                {
                    return NotFound(new { success = false, message = "Mod not found" });
                }

                return Ok(new { success = true, mod });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving mod with id: {id}");
                return StatusCode(500, new { success = false, message = $"Failed to retrieve mod: {ex.Message}" });
            }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteMod(string id)
        {
            try
            {
                var mods = _modService.LoadMods();
                var mod = mods.FirstOrDefault(m => m.UniqueId == id || m.FileName == id);

                if (mod == null)
                {
                    return NotFound(new { success = false, message = "Mod not found" });
                }

                bool deleted = _modService.DeleteMod(mod.FilePath);

                if (deleted)
                {
                    return Ok(new { success = true, message = "Mod deleted successfully" });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Failed to delete mod" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting mod with id: {id}");
                return StatusCode(500, new { success = false, message = $"Failed to delete mod: {ex.Message}" });
            }
        }

        [HttpPost("refresh")]
        public IActionResult RefreshMods()
        {
            try
            {
                var mods = _modService.LoadMods();
                _logger.LogInformation($"Refreshed mods list: {mods.Count} mods found");
                return Ok(new { success = true, mods });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing mods");
                return StatusCode(500, new { success = false, message = $"Failed to refresh mods: {ex.Message}" });
            }
        }

        [HttpPost("toggle")]
        public IActionResult ToggleMod([FromBody] ToggleModRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.FileName))
                {
                    return BadRequest(new { success = false, message = "File name is required" });
                }

                bool toggled = _modService.ToggleMod(request.FileName);

                if (toggled)
                {
                    return Ok(new { success = true, message = "Mod toggled successfully" });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Failed to toggle mod" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error toggling mod: {request?.FileName}");
                return StatusCode(500, new { success = false, message = $"Failed to toggle mod: {ex.Message}" });
            }
        }
        
        [HttpGet("game-status")]
        public IActionResult GetGameStatus()
        {
            try
            {
                var launcherLogger = _logger as ILogger<LauncherController>;
                if (launcherLogger == null)
                {
                    return StatusCode(500, new { success = false, message = "Failed to create launcher logger" });
                }
                var launcherController = new LauncherController(launcherLogger, _appConfig, _sessionTimeService);
                var gameStatus = launcherController.GetStatus();
                return gameStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting game status");
                return StatusCode(500, new { success = false, message = $"Failed to get game status: {ex.Message}" });
            }
        }
        
        [HttpGet("localized-strings")]
        public IActionResult GetLocalizedStrings()
        {
            try
            {
                var localizedStrings = new
                {
                    gameRunningWarning = _appConfig.GetLocalized("Mods:GameRunningWarning", "游戏正在运行，无法修改Mod状态"),
                    gameRunningTooltip = _appConfig.GetLocalized("Mods:GameRunningTooltip", "游戏运行时无法执行此操作"),
                    enableButton = _appConfig.GetLocalized("Mods:EnableButton", "启用"),
                    disableButton = _appConfig.GetLocalized("Mods:DisableButton", "禁用"),
                    deleteButton = _appConfig.GetLocalized("Mods:DeleteButton", "删除"),
                    authorLabel = _appConfig.GetLocalized("AuthorLabel", "作者"),
                    descriptionLabel = _appConfig.GetLocalized("DescriptionLabel", "Mod Desc")
                };
                
                return Ok(localizedStrings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting localized strings");
                return StatusCode(500, new { success = false, message = $"Failed to get localized strings: {ex.Message}" });
            }
        }
        
        [HttpPost("install")]
        public IActionResult InstallMod([FromBody] InstallModRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.FilePath))
                {
                    return BadRequest(new { success = false, message = "文件路径不能为空" });
                }

                if (!System.IO.File.Exists(request.FilePath))
                {
                    return BadRequest(new { success = false, message = "文件不存在" });
                }

                bool installed = _modService.InstallMod(request.FilePath);

                if (installed)
                {
                    return Ok(new { success = true, message = "Mod 安装成功" });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Mod 安装失败" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error installing mod from: {request?.FilePath}");
                return StatusCode(500, new { success = false, message = $"安装 Mod 时出错: {ex.Message}" });
            }
        }

        [HttpPost("check-updates")]
        public async Task<IActionResult> CheckForUpdates()
        {
            try
            {
                var mods = _modService.LoadMods();
                var updatedMods = await _modUpdateService.CheckForModUpdatesAsync(mods);
                
                var modsWithUpdates = updatedMods.Where(m => m.HasUpdateAvailable).ToList();
                _logger.LogInformation($"Found {modsWithUpdates.Count} mods with available updates out of {updatedMods.Count} total mods");
                
                return Ok(new { 
                    success = true, 
                    mods = updatedMods,
                    updateCount = modsWithUpdates.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for mod updates");
                return StatusCode(500, new { success = false, message = $"检查更新时出错: {ex.Message}" });
            }
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateMod([FromBody] UpdateModRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.FileName))
                {
                    return BadRequest(new { success = false, message = "文件名不能为空" });
                }

                // Load all mods
                var mods = _modService.LoadMods();
                var mod = mods.FirstOrDefault(m => m.UniqueId == request.FileName || m.FileName == request.FileName);

                if (mod == null)
                {
                    return NotFound(new { success = false, message = "Mod not found" });
                }

                // Check for updates for this specific mod to ensure we have the latest update info
                var modList = new List<ModInfo> { mod };
                var updatedMods = await _modUpdateService.CheckForModUpdatesAsync(modList);
                var updatedMod = updatedMods.FirstOrDefault();

                // Check if update is available using the freshly checked info
                if (updatedMod == null || !updatedMod.HasUpdateAvailable || string.IsNullOrEmpty(updatedMod.DownloadUrl))
                {
                    var message = _appConfig.GetLocalized("Mods:NoUpdateAvailable", "该 Mod 没有可用的更新");
                    return BadRequest(new { success = false, message = message });
                }

                bool updated = await _modUpdateService.UpdateModAsync(updatedMod);

                if (updated)
                {
                    return Ok(new { 
                        success = true, 
                        message = $"Mod {mod.Name} 更新成功",
                        newVersion = updatedMod.LatestVersion
                    });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Mod 更新失败" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating mod {request?.FileName}");
                return StatusCode(500, new { success = false, message = $"更新 Mod 时出错: {ex.Message}" });
            }
        }

        [HttpGet("update-progress/{id}")]
        public IActionResult GetUpdateProgress(string id)
        {
            try
            {
                var progress = ModUpdateService.GetUpdateProgress(id);
                if (progress == null)
                {
                    return NotFound(new { success = false, message = "No update in progress" });
                }

                return Ok(new { success = true, progress });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting update progress for mod {id}");
                return StatusCode(500, new { success = false, message = $"获取更新进度时出错: {ex.Message}" });
            }
        }
    }

    public class ToggleModRequest
    {
        public string FileName { get; set; } = string.Empty;
    }
    
    public class InstallModRequest
    {
        public string FilePath { get; set; } = string.Empty;
    }
    
    public class UpdateModRequest
    {
        public string FileName { get; set; } = string.Empty;
    }
}
