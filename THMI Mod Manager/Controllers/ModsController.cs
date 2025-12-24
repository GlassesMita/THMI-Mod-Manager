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

        public ModsController(ILogger<ModsController> logger, ModService modService)
        {
            _logger = logger;
            _modService = modService;
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
    }

    public class ToggleModRequest
    {
        public string FileName { get; set; }
    }
}
