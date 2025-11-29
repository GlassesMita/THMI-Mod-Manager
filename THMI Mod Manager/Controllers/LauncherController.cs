using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;

namespace THMI_Mod_Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LauncherController : ControllerBase
    {
        private readonly ILogger<LauncherController> _logger;
        private const string STEAM_APP_ID = "1584090";
        private const string PROCESS_NAME = "Touhou Mystia Izakaya";
        private const string STEAM_EXE_NAME = "steam.exe";

        public LauncherController(ILogger<LauncherController> logger)
        {
            _logger = logger;
        }

        [HttpPost("launch")]
        public async Task<IActionResult> Launch()
        {
            try
            {
                _logger.LogInformation("尝试启动游戏...");
                
                if (IsProcessRunning())
                {
                    _logger.LogWarning("游戏进程已在运行中");
                    return BadRequest("进程已经在运行中");
                }

                // 使用Steam协议启动游戏
                var steamUrl = $"steam://rungameid/{STEAM_APP_ID}";
                _logger.LogInformation($"使用Steam URL: {steamUrl}");
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = steamUrl,
                        UseShellExecute = true,
                        CreateNoWindow = true
                    };
                    _logger.LogInformation("Windows平台：启动进程配置完成，开始启动...");
                    var process = Process.Start(psi);
                    _logger.LogInformation($"进程启动结果: {(process != null ? "成功" : "失败")}");
                    if (process != null)
                    {
                        _logger.LogInformation($"进程ID: {process.Id}");
                    }
                }
                else
                {
                    // 对于其他平台，尝试使用xdg-open
                    var psi = new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = steamUrl,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    _logger.LogInformation("非Windows平台：启动进程配置完成，开始启动...");
                    var process = Process.Start(psi);
                    _logger.LogInformation($"进程启动结果: {(process != null ? "成功" : "失败")}");
                    if (process != null)
                    {
                        _logger.LogInformation($"进程ID: {process.Id}");
                    }
                }
                
                return Ok(new { success = true, message = "游戏启动命令已发送" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动进程时出错");
                return StatusCode(500, new { success = false, message = $"启动失败: {ex.Message}" });
            }
        }

        [HttpPost("stop")]
        public IActionResult Stop()
        {
            try
            {
                if (!IsProcessRunning())
                {
                    return BadRequest("进程未运行");
                }

                var processes = Process.GetProcessesByName(PROCESS_NAME);
                int stoppedCount = 0;
                
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        stoppedCount++;
                        _logger.LogInformation($"已停止进程: {PROCESS_NAME} (PID: {process.Id})");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"停止进程 {PROCESS_NAME} 时出错");
                    }
                }

                if (stoppedCount > 0)
                {
                    return Ok(new { success = true, message = $"已停止 {stoppedCount} 个进程", stoppedCount });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "停止进程失败" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止进程时出错");
                return StatusCode(500, new { success = false, message = "停止进程失败" });
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
                _logger.LogInformation($"检测到 {processes.Length} 个游戏进程");
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查进程状态时出错");
                return false;
            }
        }

        private string FindSteamExecutable()
        {
            try
            {
                // 常见Steam安装路径
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
                        _logger.LogInformation($"找到Steam可执行文件: {path}");
                        return path;
                    }
                }

                // 如果文件不存在，尝试通过进程查找
                var steamProcesses = Process.GetProcessesByName("steam");
                if (steamProcesses.Length > 0)
                {
                    try
                    {
                        var steamPath = steamProcesses[0].MainModule?.FileName;
                        if (!string.IsNullOrEmpty(steamPath))
                        {
                            _logger.LogInformation($"通过进程找到Steam可执行文件: {steamPath}");
                            return steamPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"无法获取Steam进程路径: {ex.Message}");
                    }
                }

                _logger.LogWarning("未找到Steam可执行文件");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查找Steam可执行文件时出错");
                return null;
            }
        }
    }
}