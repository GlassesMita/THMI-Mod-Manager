using Microsoft.AspNetCore.Mvc;
using THMI_Mod_Manager.Services;
using THMI_Mod_Manager.Models;

namespace THMI_Mod_Manager.Controllers
{
    /// <summary>
    /// API controller for mod operations / 模组操作的 API 控制器
    /// Provides endpoints for loading, toggling, deleting and updating mods
    /// / 提供加载、切换、删除和更新模组的端点
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ModsController : ControllerBase
    {
        private readonly ModService _modService;
        private readonly ModUpdateService _modUpdateService;
        private readonly AppConfigManager _appConfig;
        private readonly SessionTimeService _sessionTimeService;

        /// <summary>
        /// Constructor / 构造函数
        /// </summary>
        /// <param name="modService">Mod service instance / 模组服务实例</param>
        /// <param name="modUpdateService">Mod update service instance / 模组更新服务实例</param>
        /// <param name="appConfig">App config manager / 应用程序配置管理器</param>
        /// <param name="sessionTimeService">Session time service / 会话时间服务</param>
        public ModsController(ModService modService, ModUpdateService modUpdateService, AppConfigManager appConfig, SessionTimeService sessionTimeService)
        {
            _modService = modService;
            _modUpdateService = modUpdateService;
            _appConfig = appConfig;
            _sessionTimeService = sessionTimeService;
        }

        /// <summary>
        /// Get all mods / 获取所有模组
        /// </summary>
        /// <returns>List of all mods / 所有模组的列表</returns>
        [HttpGet]
        public IActionResult GetMods()
        {
            try
            {
                var mods = _modService.LoadMods();
                Logger.LogInfo($"Retrieved {mods.Count} mods");
                return Ok(new { success = true, mods });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error retrieving mods");
                return StatusCode(500, new { success = false, message = $"Failed to retrieve mods: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get a specific mod by ID or filename / 通过 ID 或 文件名获取特定模组
        /// </summary>
        /// <param name="id">Mod unique ID or filename / 模组唯一 ID 或 文件名</param>
        /// <returns>Mod information / 模组信息</returns>
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
                Logger.LogException(ex, $"Error retrieving mod with id: {id}");
                return StatusCode(500, new { success = false, message = $"Failed to retrieve mod: {ex.Message}" });
            }
        }

        /// <summary>
        /// Delete a mod by ID / 通过ID删除模组
        /// </summary>
        /// <param name="id">Mod unique ID or filename / 模组唯一 ID 或 文件名</param>
        /// <returns>Success status / 成功状态</returns>
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
                Logger.LogException(ex, $"Error deleting mod with id: {id}");
                return StatusCode(500, new { success = false, message = $"Failed to delete mod: {ex.Message}" });
            }
        }

        /// <summary>
        /// Refresh the mods list / 刷新模组列表
        /// </summary>
        /// <returns>Updated list of mods / 更新后的模组列表</returns>
        [HttpPost("refresh")]
        public IActionResult RefreshMods()
        {
            try
            {
                var mods = _modService.LoadMods();
                Logger.LogInfo($"Refreshed mods list: {mods.Count} mods found");
                return Ok(new { success = true, mods });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error refreshing mods");
                return StatusCode(500, new { success = false, message = $"Failed to refresh mods: {ex.Message}" });
            }
        }

        /// <summary>
        /// Toggle mod enabled/disabled state / 切换模组启用/禁用状态
        /// </summary>
        /// <param name="request">Toggle request containing file name / 包含文件名的切换请求</param>
        /// <returns>Success status / 成功状态</returns>
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
                Logger.LogException(ex, $"Error toggling mod: {request?.FileName}");
                return StatusCode(500, new { success = false, message = $"Failed to toggle mod: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get current game running status / 获取当前游戏运行状态
        /// </summary>
        /// <returns>Game status information / 游戏状态信息</returns>
        [HttpGet("game-status")]
        public IActionResult GetGameStatus()
        {
            try
            {
                var launcherController = new LauncherController(_appConfig, _sessionTimeService);
                var gameStatus = launcherController.GetStatus();
                return gameStatus;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error getting game status");
                return StatusCode(500, new { success = false, message = $"Failed to get game status: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get localized strings for the UI / 获取 UI 的本地化字符串
        /// </summary>
        /// <returns>Localized string dictionary / 本地化字符串字典</returns>
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
                Logger.LogException(ex, "Error getting localized strings");
                return StatusCode(500, new { success = false, message = $"Failed to get localized strings: {ex.Message}" });
            }
        }

        /// <summary>
        /// Install a mod from a local zip file / 从本地 zip 文件安装模组
        /// </summary>
        /// <param name="request">Install request containing file path / 包含文件路径的安装请求</param>
        /// <returns>Success status / 成功状态</returns>
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
                Logger.LogException(ex, $"Error installing mod from: {request?.FilePath}");
                return StatusCode(500, new { success = false, message = $"安装 Mod 时出错: {ex.Message}" });
            }
        }

        /// <summary>
        /// Check for updates for all mods / 检查所有模组的更新
        /// </summary>
        /// <returns>List of mods with update information / 包含更新信息的模组列表</returns>
        [HttpPost("check-updates")]
        public async Task<IActionResult> CheckForUpdates()
        {
            try
            {
                var mods = _modService.LoadMods();
                var updatedMods = await _modUpdateService.CheckForModUpdatesAsync(mods);
                
                var modsWithUpdates = updatedMods.Where(m => m.HasUpdateAvailable).ToList();
                Logger.LogInfo($"Found {modsWithUpdates.Count} mods with available updates out of {updatedMods.Count} total mods");
                
                return Ok(new { 
                    success = true, 
                    mods = updatedMods,
                    updateCount = modsWithUpdates.Count
                });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error checking for mod updates");
                return StatusCode(500, new { success = false, message = $"检查更新时出错: {ex.Message}" });
            }
        }

        /// <summary>
        /// Update a specific mod / 更新特定模组
        /// </summary>
        /// <param name="request">Update request containing file name / 包含文件名的更新请求</param>
        /// <returns>Success status / 成功状态</returns>
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
                Logger.LogException(ex, $"Error updating mod {request?.FileName}");
                return StatusCode(500, new { success = false, message = $"更新 Mod 时出错: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get update progress for a specific mod / 获取特定模组的更新进度
        /// </summary>
        /// <param name="id">Mod file name / 模组文件名</param>
        /// <returns>Update progress information / 更新进度信息</returns>
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
                Logger.LogException(ex, $"Error getting update progress for mod {id}");
                return StatusCode(500, new { success = false, message = $"获取更新进度时出错: {ex.Message}" });
            }
        }
    }

    /// <summary>
    /// Request model for toggling mod state / 切换模组状态的请求模型
    /// </summary>
    public class ToggleModRequest
    {
        /// <summary>File name of the mod to toggle / 要切换的模组文件名</summary>
        public string FileName { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Request model for installing a mod / 安装模组的请求模型
    /// </summary>
    public class InstallModRequest
    {
        /// <summary>Path to the mod zip file / 模组 zip 文件的路径</summary>
        public string FilePath { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Request model for updating a mod / 更新模组的请求模型
    /// </summary>
    public class UpdateModRequest
    {
        /// <summary>File name of the mod to update / 要更新的模组文件名</summary>
        public string FileName { get; set; } = string.Empty;
    }
}
