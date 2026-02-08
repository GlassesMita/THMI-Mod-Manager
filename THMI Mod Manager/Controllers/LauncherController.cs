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
        private readonly AppConfigManager _appConfig;
        private readonly SessionTimeService _sessionTimeService;
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

#if !NETFRAMEWORK && !NETSTANDARD && !NETCOREAPP
        // POSIX imports for cross-platform support
        [DllImport("libc", SetLastError = true)]
        private static extern int geteuid();
#endif

        public LauncherController(AppConfigManager appConfig, SessionTimeService sessionTimeService)
        {
            _appConfig = appConfig;
            _sessionTimeService = sessionTimeService;
            
            // 启动时检查权限（已禁用以防止权限问题）
            // TryElevatePermissions();
        }

        [HttpPost("launch")]
        public async Task<IActionResult> Launch()
        {
            try
            {
                Logger.LogInfo("Attempting to launch game...");

                if (IsProcessRunning())
                {
                    Logger.LogWarning("Game process is already running");
                    return BadRequest("Process is already running");
                }

                _sessionTimeService.StartSession();
                Logger.LogInfo("Session time tracking started");

                var launchModeValue = _appConfig.Get("[Game]LaunchMode", "steam_launch");
                string launchMode = launchModeValue ?? "steam_launch";
                
                var userSpecifiedExePathValue = _appConfig.Get("[Game]LauncherPath", "");
                string userSpecifiedExePath = userSpecifiedExePathValue ?? "";

                Logger.LogInfo($"Launch mode: {launchMode}");

                if (launchMode == "steam_launch")
                {
                    if (!IsSteamRunning())
                    {
                        Logger.LogInfo("Steam is not running, will attempt to start Steam and then launch the game");
                        bool steamStarted = await StartSteamAsync();
                        if (!steamStarted)
                        {
                            Logger.LogWarning("Failed to start Steam, but will attempt to launch game anyway");
                        }
                        else
                        {
                            await Task.Delay(3000);
                        }
                    }
                    else
                    {
                        Logger.LogInfo("Steam is already running");
                    }

                    // Launch game using Steam official protocol
                    var steamUrl = $"steam://rungameid/{STEAM_APP_ID}";
                    Logger.LogInfo($"[Compliance Notice] Initiating Steam official protocol: {steamUrl}, user must ensure they have valid authorization for this game");

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
                                Logger.LogInfo("Windows: Steam protocol launch configuration complete, starting...");
                                var process = Process.Start(psi);
                                Logger.LogInfo($"Process launch result: {(process != null ? "Success" : "Failed")}");
                                
                                // Note: Steam protocol launch doesn't give us the actual game process
                                // So we need to search by process name after the game starts
                                if (process != null)
                                {
                                    Logger.LogInfo($"Steam launcher process ID: {process.Id}");
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
                                        Logger.LogException(ex, "Error in background window title modification");
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
                            Logger.LogInfo("Non-Windows: Steam protocol launch configuration complete, starting...");
                            var process = Process.Start(psi);
                            Logger.LogInfo($"Process launch result: {(process != null ? "Success" : "Failed")}");
                            if (process != null)
                            {
                                Logger.LogInfo($"Process ID: {process.Id}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, "Exception occurred during Steam protocol launch");
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
                            Logger.LogError("[Compliance Notice] User-specified external program path not found, please select a valid authorized program path in settings");
                            return StatusCode(500, new { success = false, message = "User-specified external program path not found, please select in settings" });
                        }

                        if (!System.IO.File.Exists(userSpecifiedExePath))
                        {
                            Logger.LogError($"[Compliance Notice] User-specified external program path does not exist: {userSpecifiedExePath}");
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
                            Logger.LogInfo($"[Compliance Notice] Windows: Launching user-specified external program {userSpecifiedExePath}, user must ensure they have valid authorization for this program, this tool assumes no related liability");
                            Logger.LogInfo("Windows: User-specified external program launch configuration complete, starting...");
                            var process = Process.Start(psi);
                            Logger.LogInfo($"Process launch result: {(process != null ? "Success" : "Failed")}");
                            if (process != null)
                            {
                                Logger.LogInfo($"Process ID: {process.Id}");
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
                                    Logger.LogException(ex, "Error in background window title modification");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.LogException(ex, $"Exception occurred during user-specified external program launch: {userSpecifiedExePath}");
                            return StatusCode(500, new { success = false, message = $"External program launch failed: {ex.Message}" });
                        }
                    }
                    else
                    {
                        // Non-Windows platforms do not support user-specified external program launch
                        Logger.LogError("Non-Windows platforms do not support user-specified external program launch");
                        return StatusCode(500, new { success = false, message = "Non-Windows platforms do not support user-specified external program launch" });
                    }
                }
                
                return Ok(new { success = true, message = "Game launch command sent" });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error occurred while launching process");
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

                _sessionTimeService.StopSession();
                Logger.LogInfo("Session time tracking stopped");

                var processes = Process.GetProcessesByName(PROCESS_NAME);
                int stoppedCount = 0;

                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        stoppedCount++;
                        Logger.LogInfo($"Stopped process: {PROCESS_NAME} (PID: {process.Id})");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, $"Error occurred while stopping process {PROCESS_NAME}");
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
                Logger.LogException(ex, "Error occurred while stopping process");
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

        [HttpGet("network-status")]
        public async Task<IActionResult> GetNetworkStatus()
        {
            try
            {
                var hasEthernet = false;
                var hasWifi = false;
                var wifiSsid = "";
                var wifiSignalStrength = 0;
                var isConnected = false;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                        
                        foreach (var networkInterface in networkInterfaces)
                        {
                            if (networkInterface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                                continue;
                            
                            var networkInterfaceType = networkInterface.NetworkInterfaceType;
                            var interfaceName = networkInterface.Name;
                            
                            if (networkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet)
                            {
                                // 排除虚拟以太网适配器
                                if (IsVirtualAdapter(interfaceName))
                                    continue;
                                
                                var properties = networkInterface.GetIPProperties();
                                bool hasValidAddress = properties.UnicastAddresses.Any(
                                    addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                                
                                if (hasValidAddress)
                                {
                                    hasEthernet = true;
                                    isConnected = true;
                                }
                            }
                            
                            if (networkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                            {
                                hasWifi = true;
                                wifiSsid = GetWirelessSSID();
                                wifiSignalStrength = 75;
                                isConnected = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "获取网络接口信息时出错");
                    }
                }

                // 测试 Internet 连接
                var hasInternet = await TestInternetConnectionAsync();

                return Ok(new
                {
                    hasEthernet,
                    hasWifi,
                    wifiSsid,
                    wifiSignalStrength,
                    hasInternet,
                    isConnected
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "获取网络状态时发生错误");
                return Ok(new
                {
                    hasEthernet = false,
                    hasWifi = false,
                    wifiSsid = "",
                    wifiSignalStrength = 0,
                    hasInternet = false,
                    isConnected = false,
                    error = ex.Message
                });
            }
        }
        
        private string GetWirelessSSID()
        {
            try
            {
                // 尝试使用 netsh 获取 SSID
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "wlan show interfaces",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8
                    }
                };
                
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                // 解析 SSID - 查找 "SSID" 行（不包括 BSSID）
                foreach (var line in output.Split('\n'))
                {
                    var trimmedLine = line.Trim();
                    // 匹配 "SSID                   : NetworkName" 格式
                    if (trimmedLine.StartsWith("SSID") && !trimmedLine.Contains("BSSID"))
                    {
                        var colonIndex = trimmedLine.IndexOf(':');
                        if (colonIndex >= 0 && colonIndex < trimmedLine.Length - 1)
                        {
                            var ssid = trimmedLine.Substring(colonIndex + 1).Trim();
                            if (!string.IsNullOrWhiteSpace(ssid))
                            {
                                return ssid;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "无法使用 netsh 获取无线网络 SSID");
            }
            
            // 备用：尝试使用网络接口名称
            try
            {
                var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in networkInterfaces)
                {
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 &&
                        ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    {
                        return ni.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "无法使用备用方法获取无线网络 SSID");
            }
            
            return "";
        }

        private bool IsVirtualAdapter(string adapterName)
        {
            if (string.IsNullOrEmpty(adapterName))
                return true;
            
            var lowerName = adapterName.ToLowerInvariant();
            
            var virtualKeywords = new[]
            {
                "vethernet",
                "virtual",
                "vpn",
                "hyper-v",
                "hyperv",
                "vmware",
                "docker",
                "tunnel",
                "isatap",
                "teredo",
                "microsoft wi-fi direct virtual",
                "ring network",
                "npipe"
            };
            
            foreach (var keyword in virtualKeywords)
            {
                if (lowerName.Contains(keyword))
                    return true;
            }
            
            return false;
        }

        private async Task<bool> TestInternetConnectionAsync()
        {
            var testUrls = new[]
            {
                "https://www.bing.com/",
                "https://www.baidu.com/",
                "https://www.microsoft.com/"
            };

            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(3);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("THMI Mod Manager/1.0");

            foreach (var url in testUrls)
            {
                try
                {
                    var response = await httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                }
                catch
                {
                    // 继续尝试下一个 URL
                    continue;
                }
            }

            return false;
        }

        [HttpGet("session-time")]
        public IActionResult GetSessionTime()
        {
            var state = _sessionTimeService.GetState();
            return Ok(new { 
                isRunning = state.IsRunning, 
                formattedTime = state.FormattedTime,
                totalSeconds = state.TotalSeconds 
            });
        }

        [HttpPost("session-time/reset")]
        public IActionResult ResetSessionTime()
        {
            _sessionTimeService.ResetSession();
            return Ok(new { success = true, message = "Session time reset" });
        }

        [HttpGet("permissions")]
        public IActionResult CheckPermissions()
        {
            try
            {
                var isAdmin = PermissionHelper.IsAdministrator();
                var permissionStatus = PermissionHelper.GetPermissionStatus();
                
                Logger.LogInfo($"权限检查请求 - 管理员权限: {isAdmin}");
                
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
                Logger.LogError(ex, "权限检查失败");
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

                Logger.LogInfo("用户请求提升权限");
                
                // 尝试重新启动为管理员
                bool success = PermissionHelper.RestartAsAdministrator();
                
                if (success)
                {
                    Logger.LogInfo("权限提升成功，程序将重新启动");
                    
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
                    Logger.LogWarning("用户取消了权限提升或提升失败");
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
                Logger.LogError(ex, "权限提升失败");
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
                    Logger.LogDebug($"Detected {processes.Length} game process(es)");
                }
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occurred while checking process status");
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
                        Logger.LogDebug("Steam executable not found, using basic process detection");
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
                Logger.LogError(ex, "Error occurred while checking Steam process status");
                return false;
            }
        }

        private async Task<bool> StartSteamAsync()
        {
            try
            {
                Logger.LogInfo("Attempting to start Steam...");

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
                        Logger.LogInfo($"Steam launch process started with PID: {process.Id}");

                        await Task.Delay(5000);

                        if (!process.HasExited)
                        {
                            Logger.LogInfo("Steam appears to be starting successfully");
                            return true;
                        }
                        else
                        {
                            var error = await process.StandardError.ReadToEndAsync();
                            Logger.LogError($"Steam launch failed. Exit code: {process.ExitCode}, Error: {error}");
                            return false;
                        }
                    }
                    else
                    {
                        Logger.LogError("Failed to start Steam process");
                        return false;
                    }
                }
                else
                {
                    Logger.LogWarning("Steam auto-start is only supported on Windows");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occurred while starting Steam");
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
                            Logger.LogDebug($"Unable to get Steam process path: {ex.Message}");
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occurred while finding Steam executable");
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
                Logger.LogInfo("窗口标题修改功能已禁用，跳过标题修改");
                return;
            }

            try
            {
                // 检查管理员权限
                if (!PermissionHelper.IsAdministrator())
                {
                    Logger.LogWarning("当前程序没有管理员权限，可能导致窗口标题修改失败");
                    Logger.LogWarning("建议以管理员身份运行此程序");
                    Logger.LogWarning("Discord 叠加面板若报告错误则表明存在权限问题");
                    
                    // 提供更详细的权限状态信息
                    var permissionStatus = PermissionHelper.GetPermissionStatus();
                    Logger.LogInfo($"权限状态: {permissionStatus}");
                }
                else
                {
                    Logger.LogInfo("程序以管理员权限运行，应该可以正常修改窗口标题");
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
                            Logger.LogWarning("无法修改目标游戏进程 - 权限不足");
                            Logger.LogWarning($"目标进程PID: {gameProcesses[0].Id}, 名称: {gameProcesses[0].ProcessName}");
                            Logger.LogWarning("建议：以管理员身份重新运行此程序");
                            return; // 退出修改尝试
                        }
                        
                        Logger.LogInfo($"找到游戏进程: {gameProcesses[0].ProcessName} (PID: {gameProcesses[0].Id})");
                        
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
                                                Logger.LogInfo($"游戏窗口标题已修改: '{originalTitle}' -> '{newTitle}'");
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
                                                
                                                Logger.LogWarning($"SetWindowText 失败 (错误代码: {errorCode}): {errorMessage}");
                                                
                                                if (consecutiveFailures >= 5)
                                                {
                                                    Logger.LogWarning($"连续 {consecutiveFailures} 次修改失败");
                                                    Logger.LogWarning("可能的原因：");
                                                    Logger.LogWarning("1. 程序没有管理员权限");
                                                    Logger.LogWarning("2. 目标窗口属于更高权限的进程");
                                                    Logger.LogWarning("3. 目标窗口被其他程序保护");
                                                    Logger.LogWarning("4. 游戏使用了自定义窗口管理器");
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
                                    Logger.LogDebug($"检测到非目标窗口: '{originalTitle}'，继续搜索");
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
                    Logger.LogWarning("未能找到游戏窗口或修改标题 (超时 60 秒)");
                    
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
                Logger.LogError(ex, "修改游戏窗口标题时发生错误");
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
#if NETFRAMEWORK || NETSTANDARD || NETCOREAPP
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    WindowsIdentity identity = WindowsIdentity.GetCurrent();
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
                return false;
#else
                // Cross-platform: Use POSIX check on Unix-like systems
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || 
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return geteuid() == 0;
                }
                return false;
#endif
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
                    Logger.LogInfo("正在尝试请求管理员权限...");
                    Logger.LogInfo("请确保在UAC提示时点击'是'以授予权限");
                    
                    // 记录当前进程的详细信息
                    var currentProcess = Process.GetCurrentProcess();
                    Logger.LogInfo($"当前进程: {currentProcess.ProcessName} (PID: {currentProcess.Id})");
                    Logger.LogInfo($"启动时间: {currentProcess.StartTime}");
                    Logger.LogInfo($"工作目录: {Environment.CurrentDirectory}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"权限检查失败: {ex.Message}");
            }
        }


    }
}