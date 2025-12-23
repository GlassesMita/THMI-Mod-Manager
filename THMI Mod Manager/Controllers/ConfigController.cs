using Microsoft.AspNetCore.Mvc;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfigController : ControllerBase
    {
        private readonly AppConfigManager _appConfig;
        private readonly ILogger<ConfigController> _logger;

        public ConfigController(AppConfigManager appConfig, ILogger<ConfigController> logger)
        {
            _appConfig = appConfig;
            _logger = logger;
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
                _logger.LogError(ex, $"Failed to get configuration: {key}");
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
                _logger.LogError(ex, $"Failed to set configuration: {request.Key}");
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