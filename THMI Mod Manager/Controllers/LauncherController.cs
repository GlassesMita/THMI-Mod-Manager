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
        private const string CUSTOM_LAUNCHER_NAME = "SteamCold_Loader.exe";

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
                _logger.LogInformation("尝试启动游戏...");
                
                if (IsProcessRunning())
                {
                    _logger.LogWarning("游戏进程已在运行中");
                    return BadRequest("进程已经在运行中");
                }

                // 从配置中读取游戏版本设置
                string gameVersion = _appConfig.Get("[Game]GameVersion", "legitimate");
                string customLauncherPath = _appConfig.Get("[Game]CustomLauncherPath", "");
                
                _logger.LogInformation($"游戏版本选择: {(gameVersion == "legitimate" ? "正版" : "盗版")}");
                
                if (gameVersion == "legitimate")
                {
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
                        _logger.LogInformation("Windows平台：使用Steam协议启动进程配置完成，开始启动...");
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
                        _logger.LogInformation("非Windows平台：使用Steam协议启动进程配置完成，开始启动...");
                        var process = Process.Start(psi);
                        _logger.LogInformation($"进程启动结果: {(process != null ? "成功" : "失败")}");
                        if (process != null)
                        {
                            _logger.LogInformation($"进程ID: {process.Id}");
                        }
                    }
                }
                else
                {
                    // 使用自定义启动器
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        // 使用配置中保存的自定义启动器路径
                        if (string.IsNullOrEmpty(customLauncherPath))
                        {
                            _logger.LogError("未找到自定义启动器路径，请在设置中选择");
                            return StatusCode(500, new { success = false, message = "未找到自定义启动器路径，请在设置中选择" });
                        }
                        
                        if (!System.IO.File.Exists(customLauncherPath))
                        {
                            _logger.LogError($"自定义启动器路径不存在: {customLauncherPath}");
                            return StatusCode(500, new { success = false, message = $"自定义启动器路径不存在: {customLauncherPath}" });
                        }
                        
                        var psi = new ProcessStartInfo
                        {
                            FileName = customLauncherPath,
                            UseShellExecute = true,
                            CreateNoWindow = true
                        };
                        _logger.LogInformation($"Windows平台：使用自定义启动器 {customLauncherPath} 启动进程配置完成，开始启动...");
                        var process = Process.Start(psi);
                        _logger.LogInformation($"进程启动结果: {(process != null ? "成功" : "失败")}");
                        if (process != null)
                        {
                            _logger.LogInformation($"进程ID: {process.Id}");
                        }
                    }
                    else
                    {
                        // 非Windows平台暂不支持自定义启动器
                        _logger.LogError("非Windows平台不支持自定义启动器");
                        return StatusCode(500, new { success = false, message = "非Windows平台不支持自定义启动器" });
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
                // 只在调试模式下输出详细日志，避免生产环境频繁日志输出
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    _logger.LogDebug($"检测到 {processes.Length} 个游戏进程");
                }
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

        private string FindCustomLauncher()
        {
            try
            {
                // 查找自定义启动器的可能路径
                var launcherPaths = new[]
                {
                    // 当前应用程序目录
                    Path.Combine(Directory.GetCurrentDirectory(), CUSTOM_LAUNCHER_NAME),
                    // 应用程序根目录
                    Path.Combine(AppContext.BaseDirectory, CUSTOM_LAUNCHER_NAME),
                    // 游戏安装目录（假设与Steam库位置相关）
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Touhou Mystia Izakaya", CUSTOM_LAUNCHER_NAME),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Touhou Mystia Izakaya", CUSTOM_LAUNCHER_NAME)
                };

                foreach (var path in launcherPaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        _logger.LogInformation($"找到自定义启动器: {path}");
                        return path;
                    }
                }

                // 尝试查找当前目录下的所有子目录
                var currentDir = Directory.GetCurrentDirectory();
                var allFiles = Directory.GetFiles(currentDir, CUSTOM_LAUNCHER_NAME, SearchOption.AllDirectories);
                if (allFiles.Length > 0)
                {
                    _logger.LogInformation($"通过搜索找到自定义启动器: {allFiles[0]}");
                    return allFiles[0];
                }

                _logger.LogWarning($"未找到自定义启动器: {CUSTOM_LAUNCHER_NAME}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查找自定义启动器时出错");
                return null;
            }
        }
    }
}