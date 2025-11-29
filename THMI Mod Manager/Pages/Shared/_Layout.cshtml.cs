using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using THMI_Mod_Manager.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace THMI_Mod_Manager.Pages.Shared
{
    public class LayoutModel : PageModel
    {
        private readonly AppConfigManager _appConfig;
        private readonly ILogger<LayoutModel> _logger;

        public LayoutModel(AppConfigManager appConfig, ILogger<LayoutModel> logger)
        {
            _appConfig = appConfig;
            _logger = logger;
        }

        // 按钮属性
        public string ButtonText { get; set; } = "Launch";
        public string ButtonIcon { get; set; } = "bi-play-fill";
        public string ButtonClass { get; set; } = "btn-success";
        public bool IsProcessRunning { get; set; } = false;
        
        // Steam应用ID
        public string SteamAppId { get; set; } = "1584090";
        public string ProcessName { get; set; } = "THMI";

        // 本地化文本
        public string GetLocalizedText(string key, string defaultValue = "")
        {
            return _appConfig.GetLocalized(key, defaultValue);
        }

        // 更新按钮状态
        public void UpdateButtonState()
        {
            IsProcessRunning = IsProcessRunningCheck();
            
            if (IsProcessRunning)
            {
                ButtonText = GetLocalizedText("Buttons:Stop", "Stop");
                ButtonIcon = "bi-stop-fill";
                ButtonClass = "btn-danger";
            }
            else
            {
                ButtonText = GetLocalizedText("Buttons:Launch", "Launch");
                ButtonIcon = "bi-play-fill";
                ButtonClass = "btn-success";
            }
        }

        // 检查进程是否在运行
        private bool IsProcessRunningCheck()
        {
            try
            {
                var processes = Process.GetProcessesByName(ProcessName);
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"检查进程 {ProcessName} 状态时出错");
                return false;
            }
        }

        // 启动进程
        public IActionResult OnPostLaunchProcess()
        {
            try
            {
                if (!IsProcessRunning)
                {
                    // 使用Steam协议启动游戏
                    var steamUrl = $"steam://rungameid/{SteamAppId}";
                    
                    // 记录启动尝试
                    _logger.LogInformation($"=== Process Launch Attempt ===");
                    _logger.LogInformation($"Launch Method: Steam Protocol");
                    _logger.LogInformation($"Steam URL: {steamUrl}");
                    _logger.LogInformation($"Launch Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    
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
                    
                    _logger.LogInformation($"已启动Steam应用: {SteamAppId}");
                    _logger.LogInformation($"=== Launch Complete ===");
                }
                
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动进程时出错");
                
                // 记录错误详情
                _logger.LogError($"=== Process Launch Failed ===");
                _logger.LogError($"Error: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                }
                _logger.LogError($"=== Launch Failed ===");
                
                return RedirectToPage();
            }
        }

        // 停止进程
        public IActionResult OnPostStopProcess()
        {
            try
            {
                if (IsProcessRunning)
                {
                    _logger.LogInformation($"=== Process Stop Attempt ===");
                    _logger.LogInformation($"Process Name: {ProcessName}");
                    _logger.LogInformation($"Stop Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    
                    var processes = Process.GetProcessesByName(ProcessName);
                    foreach (var process in processes)
                    {
                        try
                        {
                            process.Kill();
                            _logger.LogInformation($"已停止进程: {ProcessName} (PID: {process.Id})");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"停止进程 {ProcessName} 时出错");
                        }
                    }
                    
                    _logger.LogInformation($"=== Stop Complete ===");
                }
                else
                {
                    _logger.LogInformation($"Process {ProcessName} is not running, no action taken.");
                }
                
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止进程时出错");
                
                // 记录错误详情
                _logger.LogError($"=== Process Stop Failed ===");
                _logger.LogError($"Error: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                }
                _logger.LogError($"=== Stop Failed ===");
                
                return RedirectToPage();
            }
        }
    }
}