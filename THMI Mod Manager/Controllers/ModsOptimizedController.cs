using Microsoft.AspNetCore.Mvc;
using THMI_Mod_Manager.Services;
using THMI_Mod_Manager.Models;

namespace THMI_Mod_Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ModsOptimizedController : ControllerBase
    {
        private readonly ILogger<ModsOptimizedController> _logger;
        private readonly ModServiceOptimized _modService;
        private readonly AppConfigManager _appConfig;

        public ModsOptimizedController(
            ILogger<ModsOptimizedController> logger, 
            ModServiceOptimized modService, 
            AppConfigManager appConfig)
        {
            _logger = logger;
            _modService = modService;
            _appConfig = appConfig;
        }

        [HttpGet]
        public ActionResult<dynamic> GetMods()
        {
            try
            {
                _logger.LogInformation("Getting mods list using optimized service");
                var mods = _modService.LoadMods();
                
                // Add localization strings
                var localizedStrings = new
                {
                    authorLabel = _appConfig.GetLocalized("Mods:AuthorLabel", "作者"),
                    descriptionLabel = _appConfig.GetLocalized("Mods:DescriptionLabel", "Mod Desc"),
                    loadedStatus = _appConfig.GetLocalized("Mods:LoadedStatus", "已加载"),
                    loadFailedStatus = _appConfig.GetLocalized("Mods:LoadFailedStatus", "加载失败"),
                    enableButton = _appConfig.GetLocalized("Mods:EnableButton", "启用"),
                    disableButton = _appConfig.GetLocalized("Mods:DisableButton", "禁用"),
                    deleteButton = _appConfig.GetLocalized("Mods:DeleteButton", "删除"),
                    confirmDeleteTitle = _appConfig.GetLocalized("Mods:ConfirmDeleteTitle", "确认删除"),
                    deleteConfirm = _appConfig.GetLocalized("Mods:DeleteConfirm", "确定要删除 Mod 吗？")
                };

                _logger.LogInformation($"Successfully returned {mods.Count} mods");
                return Ok(new 
                {
                    success = true, 
                    mods = mods,
                    localizedStrings = localizedStrings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mods list");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpPost("toggle")]
        public ActionResult<dynamic> ToggleMod([FromBody] ToggleModRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.FileName))
                {
                    return BadRequest(new { success = false, message = "File name is required" });
                }

                _logger.LogInformation($"Toggling mod: {request.FileName}");
                var success = _modService.ToggleMod(request.FileName);
                _logger.LogInformation($"Mod toggle {(success ? "succeeded" : "failed")} for {request.FileName}");
                
                return Ok(new { success = success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling mod");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpDelete("{id}")]
        public ActionResult<dynamic> DeleteMod(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest(new { success = false, message = "Mod ID is required" });
                }

                // Load mods to find the mod by ID
                var mods = _modService.LoadMods();
                var mod = mods.FirstOrDefault(m => m.UniqueId == id || m.FileName == id);

                if (mod == null)
                {
                    return NotFound(new { success = false, message = "Mod not found" });
                }

                _logger.LogInformation($"Deleting mod: {mod.FilePath}");
                var success = _modService.DeleteMod(mod.FilePath);
                _logger.LogInformation($"Mod deletion {(success ? "succeeded" : "failed")} for {mod.FileName}");
                
                return Ok(new { success = success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting mod");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }
    }
}