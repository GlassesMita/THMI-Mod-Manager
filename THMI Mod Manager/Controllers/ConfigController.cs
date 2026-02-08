using Microsoft.AspNetCore.Mvc;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfigController : ControllerBase
    {
        private readonly AppConfigManager _appConfig;
        private readonly IHostApplicationLifetime _lifetime;

        public ConfigController(AppConfigManager appConfig, IHostApplicationLifetime lifetime)
        {
            _appConfig = appConfig;
            _lifetime = lifetime;
        }

        [HttpPost("exit")]
        public IActionResult ExitApplication()
        {
            try
            {
                Logger.LogInfo("Application exit requested from web interface");
                _lifetime.StopApplication();
                return Ok(new { success = true, message = "Application is shutting down" });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Failed to stop application");
                return StatusCode(500, new { success = false, message = "Failed to stop application" });
            }
        }

        [HttpGet("get")]
        public IActionResult GetConfig([FromQuery] string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    return BadRequest(new { success = false, message = "Configuration key cannot be empty" });
                }

                var value = _appConfig.Get(key);
                return Ok(new { success = true, value = value });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, $"Failed to get configuration: {key}");
                return StatusCode(500, new { success = false, message = "Failed to get configuration" });
            }
        }

        [HttpPost("set")]
        public IActionResult SetConfig([FromBody] ConfigSetRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Key))
                {
                    return BadRequest(new { success = false, message = "Configuration key cannot be empty" });
                }

                _appConfig.Set(request.Key, request.Value ?? "");
                return Ok(new { success = true, message = "Configuration saved" });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, $"Failed to set configuration: {request.Key}");
                return StatusCode(500, new { success = false, message = "Failed to set configuration" });
            }
        }
    }

    public class ConfigSetRequest
    {
        public string Key { get; set; } = "";
        public string? Value { get; set; }
    }
}