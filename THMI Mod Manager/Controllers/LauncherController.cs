using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using THMI_Mod_Manager.Services;
using System.Threading;
using System.Security.Principal;

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
        private static readonly string[] GAME_PROCESS_NAMES = new[] 
        { 
            "Touhou Mystia Izakaya",
            "TouhouMystiaIzakaya", 
            "Mystia Izakaya",
            "MystiaIzakaya"
        };
        private const string STEAM_EXE_NAME = "steam.exe";
        private const string UNITY_WINDOW_CLASS = "UnityWndClass";
        private const string MODDED_PREFIX = "Modded ";

        // Windows API imports for window manipulation
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_STYLE = -16;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_MINIMIZEBOX = 0x00020000;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public LauncherController(ILogger<LauncherController> logger, AppConfigManager appConfig)
        {
            _logger = logger;
            _appConfig = appConfig;
            
            // 启动时检查权限（已禁用以防止权限问题）
            // TryElevatePermissions();
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

                var launchModeValue = _appConfig.Get("[Game]LaunchMode", "steam_launch");
                string launchMode = launchModeValue ?? "steam_launch";
                
                var userSpecifiedExePathValue = _appConfig.Get("[Game]LauncherPath", "");
                string userSpecifiedExePath = userSpecifiedExePathValue ?? "";

                _logger.LogInformation($"Launch mode: {launchMode}");

                if (launchMode == "steam_launch")
                {
                    if (!IsSteamRunning())
                    {
                        _logger.LogInformation("Steam is not running, will attempt to start Steam and then launch the game");
                        bool steamStarted = await StartSteamAsync();
                        if (!steamStarted)
                        {
                            _logger.LogWarning("Failed to start Steam, but will attempt to launch game anyway");
                        }
                        else
                        {
                            await Task.Delay(3000);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Steam is already running");
                    }

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
                                
                                // Note: Steam protocol launch doesn't give us the actual game process
                                // So we need to search by process name after the game starts
                                if (process != null)
                                {
                                    _logger.LogInformation($"Steam launcher process ID: {process.Id}");
                                }

                                // Start background task to modify window title after launch
                                _ = Task.Run(() =>
                                {
                                    try
                                    {
                                        ModifyGameWindowTitle();
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error in background window title modification");
                                    }
                                });
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

                            // Start background task to modify window title after launch
                            _ = Task.Run(() =>
                            {
                                try
                                {
                                    ModifyGameWindowTitle();
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error in background window title modification");
                                }
                            });
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

        [HttpGet("steam-status")]
        public IActionResult GetSteamStatus()
        {
            bool isRunning = IsSteamRunning();
            return Ok(new { isRunning });
        }

        [HttpGet("permissions")]
        public IActionResult CheckPermissions()
        {
            try
            {
                var isAdmin = PermissionHelper.IsAdministrator();
                var permissionStatus = PermissionHelper.GetPermissionStatus();
                
                _logger.LogInformation($"权限检查请求 - 管理员权限: {isAdmin}");
                
                return Ok(new 
                { 
                    success = true, 
                    isAdministrator = isAdmin,
                    permissionStatus = permissionStatus,
                    currentUser = Environment.UserName,
                    processId = Process.GetCurrentProcess().Id,
                    processName = Process.GetCurrentProcess().ProcessName,
                    workingDirectory = Environment.CurrentDirectory,
                    operatingSystem = RuntimeInformation.OSDescription,
                    osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                    processArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "权限检查失败");
                return StatusCode(500, new { success = false, message = $"权限检查失败: {ex.Message}" });
            }
        }

        [HttpPost("elevate")]
        public IActionResult RequestElevation()
        {
            try
            {
                if (PermissionHelper.IsAdministrator())
                {
                    return Ok(new { success = true, message = "已经具有管理员权限", alreadyElevated = true });
                }

                _logger.LogInformation("用户请求提升权限");
                
                // 尝试重新启动为管理员
                bool success = PermissionHelper.RestartAsAdministrator();
                
                if (success)
                {
                    _logger.LogInformation("权限提升成功，程序将重新启动");
                    
                    // 通知前端程序即将重新启动
                    return Ok(new 
                    { 
                        success = true, 
                        message = "权限提升成功，程序将重新启动",
                        requiresRestart = true,
                        elevated = true
                    });
                }
                else
                {
                    _logger.LogWarning("用户取消了权限提升或提升失败");
                    return Ok(new 
                    { 
                        success = false, 
                        message = "权限提升被取消或失败",
                        requiresRestart = false,
                        elevated = false
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "权限提升失败");
                return StatusCode(500, new { success = false, message = $"权限提升失败: {ex.Message}" });
            }
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

        private bool IsSteamRunning()
        {
            try
            {
                var steamProcesses = Process.GetProcessesByName("steam");
                
                if (steamProcesses.Length == 0)
                {
                    return false;
                }

                var steamExecutable = FindSteamExecutable();
                if (string.IsNullOrEmpty(steamExecutable))
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        _logger.LogDebug("Steam executable not found, using basic process detection");
                    }
                    return steamProcesses.Length > 0;
                }

                foreach (var process in steamProcesses)
                {
                    try
                    {
                        var mainModule = process.MainModule;
                        if (mainModule != null && !string.IsNullOrEmpty(mainModule.FileName))
                        {
                            var fileName = Path.GetFileName(mainModule.FileName);
                            var directory = Path.GetDirectoryName(mainModule.FileName);
                            
                            if (fileName.Equals("steam.exe", StringComparison.OrdinalIgnoreCase) &&
                                directory != null &&
                                directory.Contains("Steam", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }

                if (System.IO.File.Exists(steamExecutable))
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking Steam process status");
                return false;
            }
        }

        private async Task<bool> StartSteamAsync()
        {
            try
            {
                _logger.LogInformation("Attempting to start Steam...");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"& \\\"$((gp 'HKLM:\\SOFTWARE\\WOW6432Node\\Valve\\Steam').InstallPath)\\steam.exe\\\"\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    var process = Process.Start(psi);
                    if (process != null)
                    {
                        _logger.LogInformation($"Steam launch process started with PID: {process.Id}");

                        await Task.Delay(5000);

                        if (!process.HasExited)
                        {
                            _logger.LogInformation("Steam appears to be starting successfully");
                            return true;
                        }
                        else
                        {
                            var error = await process.StandardError.ReadToEndAsync();
                            _logger.LogError($"Steam launch failed. Exit code: {process.ExitCode}, Error: {error}");
                            return false;
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to start Steam process");
                        return false;
                    }
                }
                else
                {
                    _logger.LogWarning("Steam auto-start is only supported on Windows");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while starting Steam");
                return false;
            }
        }

        private string? FindSteamExecutable()
        {
            try
            {
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
                        return path;
                    }
                }

                var steamProcesses = Process.GetProcessesByName("steam");
                if (steamProcesses.Length > 0)
                {
                    try
                    {
                        var steamPath = steamProcesses[0].MainModule?.FileName;
                        if (!string.IsNullOrEmpty(steamPath))
                        {
                            return steamPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (System.Diagnostics.Debugger.IsAttached)
                        {
                            _logger.LogDebug($"Unable to get Steam process path: {ex.Message}");
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while finding Steam executable");
                return null;
            }
        }

        private void ModifyGameWindowTitle()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            // Check if title modification is enabled
            var modifyTitleValue = _appConfig.Get("[Game]ModifyTitle", "true");
            bool modifyTitle = modifyTitleValue?.ToLower() != "false";
            
            if (!modifyTitle)
            {
                _logger.LogInformation("窗口标题修改功能已禁用，跳过标题修改");
                return;
            }

            try
            {
                // 检查管理员权限
                if (!PermissionHelper.IsAdministrator())
                {
                    _logger.LogWarning("当前程序没有管理员权限，可能导致窗口标题修改失败");
                    _logger.LogWarning("建议以管理员身份运行此程序");
                    _logger.LogWarning("Discord叠加面板错误也表明存在权限问题");
                    
                    // 提供更详细的权限状态信息
                    var permissionStatus = PermissionHelper.GetPermissionStatus();
                    _logger.LogInformation($"权限状态: {permissionStatus}");
                }
                else
                {
                    _logger.LogInformation("程序以管理员权限运行，应该可以正常修改窗口标题");
                }

                const int maxAttempts = 60; // 60 seconds timeout (increased from 30)
                const int delayMs = 1000; // Check every second
                const int detectionCycles = 3; // Number of detection cycles per attempt
                bool titleModified = false;
                int consecutiveFailures = 0;
                int permissionErrors = 0;

                for (int attempt = 0; attempt < maxAttempts && !titleModified; attempt++)
                {
                    // Try to find the game process using multiple possible names
                    Process[] gameProcesses = Array.Empty<Process>();
                    IntPtr targetWindow = IntPtr.Zero;

                    // Check all possible process name variations
                    foreach (string processName in GAME_PROCESS_NAMES)
                    {
                        gameProcesses = Process.GetProcessesByName(processName);
                        if (gameProcesses.Length > 0)
                        {
                            break; // Found a matching process
                        }
                    }

                    if (gameProcesses.Length > 0)
                    {
                        // 检查是否可以修改目标进程
                        if (!PermissionHelper.CanModifyProcess(gameProcesses[0]))
                        {
                            _logger.LogWarning("无法修改目标游戏进程 - 权限不足");
                            _logger.LogWarning($"目标进程PID: {gameProcesses[0].Id}, 名称: {gameProcesses[0].ProcessName}");
                            _logger.LogWarning("建议：以管理员身份重新运行此程序");
                            return; // 退出修改尝试
                        }
                        
                        _logger.LogInformation($"找到游戏进程: {gameProcesses[0].ProcessName} (PID: {gameProcesses[0].Id})");
                        
                        // Multiple detection attempts for the same process
                        for (int cycle = 0; cycle < detectionCycles && !titleModified; cycle++)
                        {
                            // Find the window associated with the specific game process
                            targetWindow = FindWindowByProcess(gameProcesses[0].Id);
                            
                            if (targetWindow != IntPtr.Zero)
                            {
                                // Get current window title
                                var currentTitle = new System.Text.StringBuilder(256);
                                GetWindowText(targetWindow, currentTitle, currentTitle.Capacity);
                                string originalTitle = currentTitle.ToString();

                                // Enhanced verification - multiple criteria for game window detection
                                bool isTargetGame = IsValidGameWindow(originalTitle);

                                if (isTargetGame)
                                {
                                    // Double-check before modification
                                    if (IsConfirmedGameWindow(targetWindow, originalTitle))
                                    {
                                        // Check if already modded
                                        if (!originalTitle.StartsWith(MODDED_PREFIX))
                                        {
                                            string newTitle = MODDED_PREFIX + originalTitle;
                                            if (SetWindowText(targetWindow, newTitle))
                                            {
                                                _logger.LogInformation($"游戏窗口标题已修改: '{originalTitle}' -> '{newTitle}'");
                                                titleModified = true;
                                                consecutiveFailures = 0;
                                                break; // Exit detection cycle on success
                                            }
                                            else
                                            {
                                                consecutiveFailures++;
                                                permissionErrors++;
                                                
                                                // 获取详细的错误信息
                                                int errorCode = Marshal.GetLastWin32Error();
                                                string errorMessage = GetWin32ErrorMessage(errorCode);
                                                
                                                _logger.LogWarning($"SetWindowText 失败 (错误代码: {errorCode}): {errorMessage}");
                                                
                                                if (consecutiveFailures >= 5)
                                                {
                                                    _logger.LogWarning($"连续 {consecutiveFailures} 次修改失败");
                                                    _logger.LogWarning("可能的原因：");
                                                    _logger.LogWarning("1. 程序没有管理员权限");
                                                    _logger.LogWarning("2. 目标窗口属于更高权限的进程");
                                                    _logger.LogWarning("3. 目标窗口被其他程序保护");
                                                    _logger.LogWarning("4. 游戏使用了自定义窗口管理器");
                                                    break;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            // Already has prefix, consider it successful
                                            titleModified = true;
                                            consecutiveFailures = 0;
                                            break;
                                        }
                                    }
                                }
                                else if (!string.IsNullOrEmpty(originalTitle))
                                {
                                    // Found a window but it's not our target game window
                                    _logger.LogDebug($"检测到非目标窗口: '{originalTitle}'，继续搜索");
                                    consecutiveFailures = 0;
                                }
                            }
                            
                            if (!titleModified && cycle < detectionCycles - 1)
                            {
                                Thread.Sleep(200); // Short delay between detection cycles
                            }
                        }
                    }

                    if (!titleModified && attempt < maxAttempts - 1)
                    {
                        Thread.Sleep(delayMs);
                    }
                }

                if (!titleModified)
                {
                    _logger.LogWarning("未能找到游戏窗口或修改标题 (超时 60 秒)");
                    
                    // 发送浏览器通知
                    var titleModifyFailed = _appConfig.GetLocalized("Notifications:TitleModifyFailed", "标题修改失败");
                    var messageModifyFailed = _appConfig.GetLocalized("Notifications:MessageModifyFailed", 
                        "无法修改游戏窗口标题。可能的原因：缺少管理员权限|目标窗口被保护|游戏使用了自定义窗口管理器");
                    var suggestionCheckPermission = _appConfig.GetLocalized("Notifications:SuggestionCheckPermission", 
                        "请尝试以管理员身份运行本程序");
                    
                    Logger.SendBrowserNotification(
                        titleModifyFailed,
                        $"{messageModifyFailed}\n\n{suggestionCheckPermission}",
                        "warning"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "修改游戏窗口标题时发生错误");
            }
        }

        private bool IsValidGameWindow(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return false;

            // Basic exclusion criteria
            if (title.StartsWith("BepInEx") || 
                title.Contains("Console") || 
                title.Contains("6.0.0") || 
                title.Contains("be.697") ||
                title.Contains("LogOutput") ||
                title.Contains("Debug") ||
                title.Contains("Terminal"))
            {
                return false;
            }

            // Must contain game keywords
            bool hasGameKeywords = title.Contains("Touhou") || title.Contains("Izakaya");
            if (!hasGameKeywords)
                return false;

            // Prefer cleaner, shorter titles (typical game window titles)
            if (title.Length > 60)
                return false;

            // Look for patterns that indicate a main game window
            // Game windows typically have cleaner titles without technical details
            bool looksLikeGameTitle = !title.Contains("-") || // No dashes (common in console titles)
                                    (title.Split('-').Length <= 2 && !title.Contains("be."));

            return looksLikeGameTitle;
        }

        private bool IsConfirmedGameWindow(IntPtr hWnd, string title)
        {
            // Additional confirmation checks
            try
            {
                // Check window visibility (game windows are typically visible)
                if (!IsWindowVisible(hWnd))
                    return false;

                // Check if window is minimized (game windows might be, but console windows often are)
                if (IsIconic(hWnd))
                {
                    // For minimized windows, require very clean title
                    return title.Length < 30 && 
                           (title.Equals("Touhou Mystia Izakaya") || 
                            title.Equals("東方夜雀食堂") ||
                            title.Equals("TouhouMystiaIzakaya"));
                }

                // For visible windows, check window style
                var style = GetWindowLong(hWnd, GWL_STYLE);
                bool hasTitleBar = (style & WS_CAPTION) == WS_CAPTION;
                bool hasMinimizeBox = (style & WS_MINIMIZEBOX) == WS_MINIMIZEBOX;
                
                // Game windows typically have title bars and minimize buttons
                return hasTitleBar && hasMinimizeBox;
            }
            catch
            {
                // If we can't get window info, fall back to title-only validation
                return IsValidGameWindow(title);
            }
        }

        private IntPtr FindWindowByProcess(int processId)
        {
            IntPtr foundWindow = IntPtr.Zero;
            IntPtr bestCandidateWindow = IntPtr.Zero;
            int bestCandidateScore = 0;
            
            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out uint windowProcessId);
                if (windowProcessId == (uint)processId)
                {
                    var windowTitle = new System.Text.StringBuilder(256);
                    GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                    string title = windowTitle.ToString();
                    
                    // Only accept windows with actual titles (not empty or just spaces)
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        // Calculate a score for this window based on how likely it is to be the game window
                        int score = CalculateWindowScore(hWnd, title);
                        
                        // Perfect match - stop searching immediately
                        if (score >= 100)
                        {
                            foundWindow = hWnd;
                            return false; // Stop enumeration
                        }
                        
                        // Track the best candidate so far
                        if (score > bestCandidateScore)
                        {
                            bestCandidateScore = score;
                            bestCandidateWindow = hWnd;
                        }
                    }
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);

            // Return perfect match if found, otherwise return best candidate
            return foundWindow != IntPtr.Zero ? foundWindow : bestCandidateWindow;
        }

        private int CalculateWindowScore(IntPtr hWnd, string title)
        {
            int score = 0;
            
            // Basic validation
            if (string.IsNullOrWhiteSpace(title))
                return 0;
            
            // Exclude obvious non-game windows
            if (title.StartsWith("BepInEx") || 
                title.Contains("Console") || 
                title.Contains("Debug") ||
                title.Contains("LogOutput") ||
                title.Contains("Terminal"))
            {
                return 0;
            }
            
            // Check window visibility (game windows should be visible)
            if (!IsWindowVisible(hWnd))
                score -= 20;
            else
                score += 10;
            
            // Check window style (game windows typically have title bars)
            try
            {
                var style = GetWindowLong(hWnd, GWL_STYLE);
                bool hasTitleBar = (style & WS_CAPTION) == WS_CAPTION;
                bool hasMinimizeBox = (style & WS_MINIMIZEBOX) == WS_MINIMIZEBOX;
                
                if (hasTitleBar) score += 15;
                if (hasMinimizeBox) score += 10;
            }
            catch
            {
                // Ignore style check errors
            }
            
            // Check for game keywords
            bool hasTouhou = title.Contains("Touhou");
            bool hasIzakaya = title.Contains("Izakaya");
            
            if (hasTouhou) score += 25;
            if (hasIzakaya) score += 25;
            
            // Perfect title matches (exact or near-exact)
            if (title.Equals("Touhou Mystia Izakaya") || 
                title.Equals("東方夜雀食堂") ||
                title.Equals("TouhouMystiaIzakaya"))
            {
                score += 100; // Perfect match
            }
            
            // Penalize technical-looking titles
            if (title.Contains("6.0.0") || title.Contains("be.697") || title.Contains("-"))
                score -= 30;
            
            // Prefer shorter, cleaner titles
            if (title.Length < 30)
                score += 20;
            else if (title.Length < 50)
                score += 10;
            else if (title.Length > 80)
                score -= 20;
            
            return Math.Max(0, score); // Ensure score is never negative
        }

        /// <summary>
        /// 检查当前是否以管理员身份运行
        /// </summary>
        private bool IsRunningAsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取Windows错误代码的详细描述
        /// </summary>
        private string GetWin32ErrorMessage(int errorCode)
        {
            try
            {
                switch (errorCode)
                {
                    case 5: return "访问被拒绝 (Access is denied) - 需要管理员权限";
                    case 1400: return "无效的窗口句柄 (Invalid window handle) - 窗口可能已关闭";
                    case 1421: return "找不到控制ID (Control ID not found)";
                    case 183: return "当文件已存在时，无法创建该文件 (Cannot create a file when that file already exists)";
                    case 998: return "对内存位置的无效访问 (Invalid access to memory location)";
                    case 87: return "参数错误 (The parameter is incorrect)";
                    case 1300: return "并非所有引用的特权或组都分配给调用方 (Not all privileges or groups are assigned to the caller)";
                    case 1314: return "客户端未持有所需的权限 (A required privilege is not held by the client)";
                    default: return $"未知错误 (Unknown error code: {errorCode})";
                }
            }
            catch
            {
                return $"错误代码 {errorCode}";
            }
        }

        /// <summary>
        /// 尝试提升权限（需要UAC确认）
        /// </summary>
        private void TryElevatePermissions()
        {
            try
            {
                if (!IsRunningAsAdministrator())
                {
                    _logger.LogInformation("正在尝试请求管理员权限...");
                    _logger.LogInformation("请确保在UAC提示时点击'是'以授予权限");
                    
                    // 记录当前进程的详细信息
                    var currentProcess = Process.GetCurrentProcess();
                    _logger.LogInformation($"当前进程: {currentProcess.ProcessName} (PID: {currentProcess.Id})");
                    _logger.LogInformation($"启动时间: {currentProcess.StartTime}");
                    _logger.LogInformation($"工作目录: {Environment.CurrentDirectory}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"权限检查失败: {ex.Message}");
            }
        }


    }
}