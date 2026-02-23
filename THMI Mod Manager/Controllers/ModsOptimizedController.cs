using Microsoft.AspNetCore.Mvc;
using THMI_Mod_Manager.Services;
using THMI_Mod_Manager.Models;

namespace THMI_Mod_Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ModsOptimizedController : ControllerBase
    {
        private readonly ModServiceOptimized _modService;
        private readonly AppConfigManager _appConfig;

        public ModsOptimizedController(
            ModServiceOptimized modService, 
            AppConfigManager appConfig)
        {
            _modService = modService;
            _appConfig = appConfig;
        }

        [HttpGet]
        public ActionResult<dynamic> GetMods()
        {
            try
            {
                Logger.LogInfo("Getting mods list using optimized service");
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

                Logger.LogInfo($"Successfully returned {mods.Count} mods");
                return Ok(new 
                {
                    success = true, 
                    mods = mods,
                    localizedStrings = localizedStrings
                });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error getting mods list");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Check for mod conflicts / 检查模组冲突
        /// </summary>
        /// <returns>List of conflicting mod pairs / 冲突模组对列表</returns>
        [HttpGet("conflicts")]
        public ActionResult<dynamic> GetConflicts()
        {
            try
            {
                var mods = _modService.LoadMods();
                var conflicts = new List<object>();
                
                foreach (var mod in mods.Where(m => !m.IsDisabled && m.IncompatibleWith.Count > 0))
                {
                    foreach (var incompatibleId in mod.IncompatibleWith)
                    {
                        var conflictingMod = mods.FirstOrDefault(m => 
                            m.UniqueId == incompatibleId && 
                            !m.IsDisabled && 
                            m.FileName != mod.FileName);
                        
                        if (conflictingMod != null)
                        {
                            conflicts.Add(new {
                                mod1 = new { name = mod.Name, uniqueId = mod.UniqueId, version = mod.Version, fileName = mod.FileName },
                                mod2 = new { name = conflictingMod.Name, uniqueId = conflictingMod.UniqueId, version = conflictingMod.Version, fileName = conflictingMod.FileName }
                            });
                        }
                    }
                }
                
                Logger.LogInfo($"Found {conflicts.Count} conflict pairs");
                return Ok(new { success = true, conflicts });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error checking conflicts");
                return StatusCode(500, new { success = false, message = $"Failed to check conflicts: {ex.Message}" });
            }
        }

        /// <summary>
        /// Toggle mod enabled/disabled state / 切换模组启用/禁用状态
        /// </summary>
        /// <param name="request">Toggle request containing file name / 包含文件名的切换请求</param>
        /// <returns>Success status and conflict information / 成功状态和冲突信息</returns>
        [HttpPost("toggle")]
        public ActionResult<dynamic> ToggleMod([FromBody] ToggleModRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.FileName))
                {
                    return BadRequest(new { success = false, message = "File name is required" });
                }

                Logger.LogInfo($"Toggling mod: {request.FileName}");
                var result = _modService.ToggleMod(request.FileName);
                Logger.LogInfo($"Mod toggle {(result.Success ? "succeeded" : "failed")} for {request.FileName}");
                
                if (result.Success)
                {
                    return Ok(new { success = true, message = "Mod toggled successfully" });
                }
                else if (result.ConflictingMods.Count > 0)
                {
                    // Return conflict information / 返回冲突信息
                    return Ok(new { 
                        success = false, 
                        message = result.ErrorMessage,
                        hasConflicts = true,
                        modBeingEnabled = result.ModBeingEnabled != null ? new {
                            name = result.ModBeingEnabled.Name,
                            uniqueId = result.ModBeingEnabled.UniqueId,
                            fileName = result.ModBeingEnabled.FileName,
                            version = result.ModBeingEnabled.Version
                        } : null,
                        conflicts = result.ConflictingMods.Select(m => new { 
                            name = m.Name, 
                            uniqueId = m.UniqueId, 
                            fileName = m.FileName,
                            version = m.Version
                        }).ToList()
                    });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = result.ErrorMessage ?? "Failed to toggle mod" });
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error toggling mod");
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

                Logger.LogInfo($"Deleting mod: {mod.FilePath}");
                var success = _modService.DeleteMod(mod.FilePath);
                Logger.LogInfo($"Mod deletion {(success ? "succeeded" : "failed")} for {mod.FileName}");
                
                return Ok(new { success = success });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error deleting mod");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }
    }
}