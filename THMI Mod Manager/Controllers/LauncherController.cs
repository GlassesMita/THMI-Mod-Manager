using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LauncherController : ControllerBase
    {
        private readonly ILogger<LauncherController> _logger;
        private readonly AppConfigManager _appConfig;
        private const string STEAM_APP_ID = "1584090";
        private const string PROCESS_NAME = "Touhou Mystia Izakaya";
        private const string STEAM_EXE_NAME = "steam.exe";

        public LauncherController(ILogger<LauncherController> logger, AppConfigManager appConfig)
        {
            _logger = logger;
            _appConfig = appConfig;
        }

        [HttpPost("launch")]
        public async Task<IActionResult> Launch()
        {
            try
            {
                _logger.LogInformation("Attempting to launch game...");

                if (IsProcessRunning())
                {
                    _logger.LogWarning("Game process is already running");
                    return BadRequest("Process is already running");
                }

                // Read launch mode from configuration
                string launchMode = _appConfig.Get("[Game]LaunchMode", "steam_launch");
                string userSpecifiedExePath = _appConfig.Get("[Game]LauncherPath", "");

                _logger.LogInformation($"Launch mode: {launchMode}");

                if (launchMode == "steam_launch")
                {
                    // Launch game using Steam official protocol
                    var steamUrl = $"steam://rungameid/{STEAM_APP_ID}";
                    _logger.LogInformation($"[Compliance Notice] Initiating Steam official protocol: {steamUrl}, user must ensure they have valid authorization for this game");

                    try
                    {
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = steamUrl,
                                UseShellExecute = true,
                                CreateNoWindow = true
                            };
                            _logger.LogInformation("Windows: Steam protocol launch configuration complete, starting...");
                            var process = Process.Start(psi);
                            _logger.LogInformation($"Process launch result: {(process != null ? "Success" : "Failed")}");
                            if (process != null)
                            {
                                _logger.LogInformation($"Process ID: {process.Id}");
                            }
                        }
                        else
                        {
                            // For other platforms, try using xdg-open
                            var psi = new ProcessStartInfo
                            {
                                FileName = "xdg-open",
                                Arguments = steamUrl,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            _logger.LogInformation("Non-Windows: Steam protocol launch configuration complete, starting...");
                            var process = Process.Start(psi);
                            _logger.LogInformation($"Process launch result: {(process != null ? "Success" : "Failed")}");
                            if (process != null)
                            {
                                _logger.LogInformation($"Process ID: {process.Id}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception occurred during Steam protocol launch");
                        return StatusCode(500, new { success = false, message = $"Steam launch failed: {ex.Message}" });
                    }
                }
                else
                {
                    // User-specified external program launch
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        // Use user-specified external program path from configuration
                        if (string.IsNullOrEmpty(userSpecifiedExePath))
                        {
                            _logger.LogError("[Compliance Notice] User-specified external program path not found, please select a valid authorized program path in settings");
                            return StatusCode(500, new { success = false, message = "User-specified external program path not found, please select in settings" });
                        }

                        if (!System.IO.File.Exists(userSpecifiedExePath))
                        {
                            _logger.LogError($"[Compliance Notice] User-specified external program path does not exist: {userSpecifiedExePath}");
                            return StatusCode(500, new { success = false, message = $"User-specified external program path does not exist: {userSpecifiedExePath}" });
                        }

                        try
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = userSpecifiedExePath,
                                UseShellExecute = true,
                                CreateNoWindow = true
                            };
                            // Core compliance log: clearly state user bears legal responsibility
                            _logger.LogInformation($"[Compliance Notice] Windows: Launching user-specified external program {userSpecifiedExePath}, user must ensure they have valid authorization for this program, this tool assumes no related liability");
                            _logger.LogInformation("Windows: User-specified external program launch configuration complete, starting...");
                            var process = Process.Start(psi);
                            _logger.LogInformation($"Process launch result: {(process != null ? "Success" : "Failed")}");
                            if (process != null)
                            {
                                _logger.LogInformation($"Process ID: {process.Id}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Exception occurred during user-specified external program launch: {userSpecifiedExePath}");
                            return StatusCode(500, new { success = false, message = $"External program launch failed: {ex.Message}" });
                        }
                    }
                    else
                    {
                        // Non-Windows platforms do not support user-specified external program launch
                        _logger.LogError("Non-Windows platforms do not support user-specified external program launch");
                        return StatusCode(500, new { success = false, message = "Non-Windows platforms do not support user-specified external program launch" });
                    }
                }
                
                return Ok(new { success = true, message = "Game launch command sent" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while launching process");
                return StatusCode(500, new { success = false, message = $"Launch failed: {ex.Message}" });
            }
        }

        [HttpPost("stop")]
        public IActionResult Stop()
        {
            try
            {
                if (!IsProcessRunning())
                {
                    return BadRequest("Process is not running");
                }

                var processes = Process.GetProcessesByName(PROCESS_NAME);
                int stoppedCount = 0;

                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        stoppedCount++;
                        _logger.LogInformation($"Stopped process: {PROCESS_NAME} (PID: {process.Id})");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error occurred while stopping process {PROCESS_NAME}");
                    }
                }

                if (stoppedCount > 0)
                {
                    return Ok(new { success = true, message = $"Stopped {stoppedCount} process(es)", stoppedCount });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Failed to stop process" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while stopping process");
                return StatusCode(500, new { success = false, message = "Failed to stop process" });
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            bool isRunning = IsProcessRunning();
            return Ok(new { isRunning });
        }

        private bool IsProcessRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName(PROCESS_NAME);
                // Only output detailed logs in debug mode to avoid frequent logging in production
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    _logger.LogDebug($"Detected {processes.Length} game process(es)");
                }
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking process status");
                return false;
            }
        }

        private string FindSteamExecutable()
        {
            try
            {
                // Common Steam installation paths
                var steamPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", STEAM_EXE_NAME),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", STEAM_EXE_NAME),
                    Path.Combine("C:\\", "Program Files", "Steam", STEAM_EXE_NAME),
                    Path.Combine("C:\\", "Program Files (x86)", "Steam", STEAM_EXE_NAME)
                };

                foreach (var path in steamPaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        _logger.LogInformation($"Found Steam executable: {path}");
                        return path;
                    }
                }

                // If file does not exist, try finding through process
                var steamProcesses = Process.GetProcessesByName("steam");
                if (steamProcesses.Length > 0)
                {
                    try
                    {
                        var steamPath = steamProcesses[0].MainModule?.FileName;
                        if (!string.IsNullOrEmpty(steamPath))
                        {
                            _logger.LogInformation($"Found Steam executable through process: {steamPath}");
                            return steamPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Unable to get Steam process path: {ex.Message}");
                    }
                }

                _logger.LogWarning("Steam executable not found");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while finding Steam executable");
                return null;
            }
        }
    }
}