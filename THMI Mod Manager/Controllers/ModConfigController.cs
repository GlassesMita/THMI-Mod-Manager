using Microsoft.AspNetCore.Mvc;
using THMI_Mod_Manager.Services;
using System.Text.RegularExpressions;

namespace THMI_Mod_Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ModConfigController : ControllerBase
    {
        private readonly AppConfigManager _appConfig;

        public ModConfigController(AppConfigManager appConfig)
        {
            _appConfig = appConfig;
        }

        private string? GetGamePath()
        {
            var gamePath = _appConfig.Get("[Game]GamePath", "");
            
            // Try to detect game path if not configured
            if (string.IsNullOrEmpty(gamePath))
            {
                // Try common Steam installation paths
                var steamPaths = new[]
                {
                    @"f:\SteamLibrary\steamapps\common\Touhou Mystia Izakaya",
                    @"c:\Program Files (x86)\Steam\steamapps\common\Touhou Mystia Izakaya",
                    @"c:\Steam\steamapps\common\Touhou Mystia Izakaya",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Steam\steamapps\common\Touhou Mystia Izakaya")
                };
                
                foreach (var path in steamPaths)
                {
                    if (Directory.Exists(path))
                    {
                        gamePath = path;
                        break;
                    }
                }
            }
            
            return gamePath;
        }

        [HttpGet("list")]
        public IActionResult ListConfigs()
        {
            try
            {
                var gamePath = this.GetGamePath();
                if (string.IsNullOrEmpty(gamePath))
                {
                    return BadRequest(new { success = false, message = "Game path not found" });
                }

                var configPath = Path.Combine(gamePath, "BepInEx", "config");
                if (!Directory.Exists(configPath))
                {
                    return Ok(new { success = true, configs = new List<object>() });
                }

                var configFiles = Directory.GetFiles(configPath, "*.cfg")
                    .Select(f => new
                    {
                        name = Path.GetFileNameWithoutExtension(f),
                        fileName = Path.GetFileName(f),
                        path = f,
                        size = new FileInfo(f).Length
                    })
                    .ToList();

                return Ok(new { success = true, configs = configFiles });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error listing config files");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("read")]
        public IActionResult ReadConfig([FromQuery] string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    return BadRequest(new { success = false, message = "File name cannot be empty" });
                }

                var gamePath = this.GetGamePath();
                if (string.IsNullOrEmpty(gamePath))
                {
                    return BadRequest(new { success = false, message = "Game path not found" });
                }

                var configPath = Path.Combine(gamePath, "BepInEx", "config", fileName);
                if (!System.IO.File.Exists(configPath))
                {
                    return NotFound(new { success = false, message = "Config file not found" });
                }

                var content = System.IO.File.ReadAllText(configPath);
                var sections = ParseConfigContent(content);

                return Ok(new { success = true, fileName = fileName, content = content, sections = sections });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, $"Error reading config file: {fileName}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("save")]
        public IActionResult SaveConfig([FromBody] SaveConfigRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.FileName) || string.IsNullOrEmpty(request.Content))
                {
                    return BadRequest(new { success = false, message = "File name and content are required" });
                }

                var gamePath = this.GetGamePath();
                if (string.IsNullOrEmpty(gamePath))
                {
                    return BadRequest(new { success = false, message = "Game path not found" });
                }

                var configPath = Path.Combine(gamePath, "BepInEx", "config", request.FileName);
                
                // Create directory if it doesn't exist
                var configDir = Path.GetDirectoryName(configPath);
                if (configDir != null && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                System.IO.File.WriteAllText(configPath, request.Content);

                Logger.LogInfo($"Config file saved: {configPath}");

                return Ok(new { success = true, message = "Config saved successfully" });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, $"Error saving config file: {request.FileName}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        private List<ConfigSection> ParseConfigContent(string content)
        {
            var sections = new List<ConfigSection>();
            var lines = content.Split('\n');
            ConfigSection? currentSection = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                    continue;

                // Check for section header
                var sectionMatch = Regex.Match(trimmed, @"^\[([^\]]+)\]$");
                if (sectionMatch.Success)
                {
                    if (currentSection != null)
                    {
                        sections.Add(currentSection);
                    }
                    currentSection = new ConfigSection
                    {
                        Name = sectionMatch.Groups[1].Value,
                        Keys = new List<ConfigKey>()
                    };
                    continue;
                }

                // Check for key-value pair
                var keyMatch = Regex.Match(trimmed, @"^([^=#]+)=(.+)$");
                if (keyMatch.Success && currentSection != null)
                {
                    var key = keyMatch.Groups[1].Value.Trim();
                    var value = keyMatch.Groups[2].Value.Trim();
                    
                    // Get comments from original line
                    var comment = "";
                    var commentMatch = Regex.Match(trimmed, @"[;#]\s*(.+)$");
                    if (commentMatch.Success)
                    {
                        comment = commentMatch.Groups[1].Value.Trim();
                    }

                    currentSection.Keys.Add(new ConfigKey
                    {
                        Name = key,
                        Value = value,
                        Comment = comment,
                        Line = trimmed
                    });
                }
            }

            if (currentSection != null)
            {
                sections.Add(currentSection);
            }

            return sections;
        }
    }

    public class SaveConfigRequest
    {
        public string FileName { get; set; } = "";
        public string Content { get; set; } = "";
    }

    public class ConfigSection
    {
        public string Name { get; set; } = "";
        public List<ConfigKey> Keys { get; set; } = new();
    }

    public class ConfigKey
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public string Comment { get; set; } = "";
        public string Line { get; set; } = "";
    }
}
