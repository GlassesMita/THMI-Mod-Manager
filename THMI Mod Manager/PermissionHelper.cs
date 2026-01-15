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
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern bool IsUserAnAdmin();

        /// <summary>
        /// 检查是否以管理员身份运行
        /// </summary>
        public static bool IsAdministrator()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    WindowsIdentity identity = WindowsIdentity.GetCurrent();
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
                return false;
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
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = currentProcess.MainModule?.FileName ?? Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName,
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
                    var identity = WindowsIdentity.GetCurrent();
                    return $"当前用户: {identity?.Name ?? "未知用户"}\n权限级别: 标准用户\n建议: 以管理员身份运行程序以获得最佳兼容性";
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
        public static bool CanModifyProcess(Process targetProcess)
        {
            try
            {
                if (targetProcess == null || targetProcess.HasExited)
                    return false;

                // 如果当前是管理员，可以修改任何进程
                if (IsAdministrator())
                    return true;

                // 检查目标进程的完整性级别
                try
                {
                    // 尝试获取进程的基本信息，如果失败说明权限不足
                    var _ = targetProcess.MainModule;
                    var __ = targetProcess.Id;
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