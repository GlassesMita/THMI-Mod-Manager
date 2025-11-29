using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace THMI_Mod_Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LauncherController : ControllerBase
    {
        private readonly ILogger<LauncherController> _logger;
        private const string STEAM_APP_ID = "1584090";
        private const string PROCESS_NAME = "Touhou Mystia Izakaya";

        public LauncherController(ILogger<LauncherController> logger)
        {
            _logger = logger;
        }

        [HttpPost("launch")]
        public IActionResult Launch()
        {
            try
            {
                if (IsProcessRunning())
                {
                    return BadRequest("进程已经在运行中");
                }

                // 使用Steam协议启动游戏
                var steamUrl = $"steam://rungameid/{STEAM_APP_ID}";
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = steamUrl,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // 对于其他平台，尝试使用xdg-open
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = steamUrl,
                        UseShellExecute = false
                    });
                }
                
                _logger.LogInformation($"已启动Steam应用: {STEAM_APP_ID}");
                return Ok(new { success = true, message = "启动成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动进程时出错");
                return StatusCode(500, new { success = false, message = "启动失败" });
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
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"检查进程 {PROCESS_NAME} 状态时出错");
                return false;
            }
        }
    }
}