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

        public LayoutModel(AppConfigManager appConfig)
        {
            _appConfig = appConfig;
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
                Logger.LogException(ex, $"检查进程 {ProcessName} 状态时出错");
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
                    Logger.LogInfo($"=== Process Launch Attempt ===");
                    Logger.LogInfo($"Launch Method: Steam Protocol");
                    Logger.LogInfo($"Steam URL: {steamUrl}");
                    Logger.LogInfo($"Launch Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    
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
                    
                    Logger.LogInfo($"已启动Steam应用: {SteamAppId}");
                    Logger.LogInfo($"=== Launch Complete ===");
                }
                
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "启动进程时出错");
                
                // 记录错误详情
                Logger.LogError($"=== Process Launch Failed ===");
                Logger.LogError($"Error: {ex.Message}");
                Logger.LogError($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                }
                Logger.LogError($"=== Launch Failed ===");
                
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
                    Logger.LogInfo($"=== Process Stop Attempt ===");
                    Logger.LogInfo($"Process Name: {ProcessName}");
                    Logger.LogInfo($"Stop Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    
                    var processes = Process.GetProcessesByName(ProcessName);
                    foreach (var process in processes)
                    {
                        try
                        {
                            process.Kill();
                            Logger.LogInfo($"已停止进程: {ProcessName} (PID: {process.Id})");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogException(ex, $"停止进程 {ProcessName} 时出错");
                        }
                    }
                    
                    Logger.LogInfo($"=== Stop Complete ===");
                }
                else
                {
                    Logger.LogInfo($"Process {ProcessName} is not running, no action taken.");
                }
                
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "停止进程时出错");
                
                // 记录错误详情
                Logger.LogError($"=== Process Stop Failed ===");
                Logger.LogError($"Error: {ex.Message}");
                Logger.LogError($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                }
                Logger.LogError($"=== Stop Failed ===");
                
                return RedirectToPage();
            }
        }
    }
}