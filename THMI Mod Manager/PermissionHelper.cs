using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager
{
    /// <summary>
    /// 权限提升帮助器
    /// </summary>
    public static class PermissionHelper
    {
#if NETFRAMEWORK || NETSTANDARD || NETCOREAPP
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern bool IsUserAnAdmin();
#else
        [DllImport("libc", SetLastError = true)]
        private static extern int geteuid();
#endif

        /// <summary>
        /// 检查是否以管理员身份运行
        /// </summary>
        public static bool IsAdministrator()
        {
            try
            {
#if NETFRAMEWORK || NETSTANDARD || NETCOREAPP
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    WindowsIdentity? identity = WindowsIdentity.GetCurrent();
                    if (identity == null)
                        return false;
                    
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
                return false;
#else
                // Cross-platform: Use POSIX check on Unix-like systems
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || 
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    int uid = geteuid();
                    return uid == 0;
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
        /// 检查是否以管理员身份运行（别名方法）
        /// </summary>
        public static bool IsRunAsAdmin()
        {
            return IsAdministrator();
        }

        /// <summary>
        /// 尝试以管理员权限重新启动当前程序
        /// </summary>
        public static bool RestartAsAdministrator()
        {
            try
            {
                if (IsAdministrator())
                {
                    return true; // 已经具有管理员权限
                }

                var currentProcess = Process.GetCurrentProcess();
                
                string? executablePath = currentProcess.MainModule?.FileName;
                if (string.IsNullOrEmpty(executablePath))
                {
                    executablePath = Environment.ProcessPath;
                }
                if (string.IsNullOrEmpty(executablePath))
                {
                    try
                    {
                        executablePath = Process.GetCurrentProcess().MainModule?.FileName;
                    }
                    catch
                    {
                        executablePath = null;
                    }
                }
                if (string.IsNullOrEmpty(executablePath))
                {
                    executablePath = AppContext.BaseDirectory?.TrimEnd('\\', '/') + 
                                    System.IO.Path.DirectorySeparatorChar + 
                                    AppDomain.CurrentDomain.FriendlyName;
                }
                
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = executablePath ?? string.Empty,
                    Verb = "runas" // 请求管理员权限
                };

                try
                {
                    Process.Start(startInfo);
                    return true;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    // 用户取消了UAC提示
                    if (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
                    {
                        Console.WriteLine("需要管理员权限才能修改游戏窗口标题。请在UAC提示时点击'是'以授予权限。");
                        Logger.LogWarning("Administrator privileges required to modify game window title. Please click 'Yes' in the UAC prompt to grant permissions.");
                    }
                    else
                    {
                        Console.WriteLine($"无法获取管理员权限：{ex.Message} (错误代码：{ex.NativeErrorCode})");
                        Logger.LogError($"Failed to obtain administrator privileges: {ex.Message} (Error code: {ex.NativeErrorCode})");
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"权限提升时发生错误：{ex.Message}");
                Logger.LogError($"Error during privilege elevation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取当前权限状态的详细描述
        /// </summary>
        public static string GetPermissionStatus()
        {
            try
            {
                if (IsAdministrator())
                {
                    return "当前程序以管理员权限运行";
                }
                else
                {
#if NETFRAMEWORK || NETSTANDARD || NETCOREAPP
                    WindowsIdentity? identity = WindowsIdentity.GetCurrent();
                    string userName = identity?.Name ?? "Unknown User";
                    return $"当前用户: {userName}\n权限级别: 标准用户\n建议: 以管理员身份运行程序以获得最佳兼容性";
#else
                    // Cross-platform: Get user info via environment
                    var userName = Environment.GetEnvironmentVariable("USER") ?? 
                                   Environment.GetEnvironmentVariable("USERNAME") ?? "Unknown";
                    int uid = geteuid();
                    bool isRoot = uid == 0;
                    return $"当前用户: {userName}\n权限级别: {(isRoot ? "管理员 (root)" : "标准用户")}\n建议: {(isRoot ? "程序以管理员权限运行" : "以管理员身份运行程序以获得最佳兼容性")}";
#endif
                }
            }
            catch (Exception ex)
            {
                return $"无法获取权限信息: {ex.Message}";
            }
        }

        /// <summary>
        /// 检查目标进程是否可以被修改
        /// </summary>
        public static bool CanModifyProcess(Process? targetProcess)
        {
            try
            {
                if (targetProcess == null)
                    return false;

                if (targetProcess.HasExited)
                    return false;

                // 如果当前是管理员，可以修改任何进程
                if (IsAdministrator())
                    return true;

                // 检查目标进程的完整性级别
                try
                {
                    // 尝试获取进程的基本信息，如果失败说明权限不足
                    ProcessModule? mainModule = targetProcess.MainModule;
                    int processId = targetProcess.Id;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}