using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace THMI_Mod_Manager.Services
{
    public class SystemInfoLogger
    {
        private readonly AppConfigManager? _appConfigManager;
        private readonly string _contentRootPath;

        public SystemInfoLogger(AppConfigManager? appConfigManager, string contentRootPath)
        {
            _appConfigManager = appConfigManager;
            _contentRootPath = contentRootPath;
        }

        public void LogApplicationStartup()
        {
            try
            {
                Logger.LogInfo("*** Initialized Application ***");

                if (WineEnv.IsWine)
                {
                    Logger.LogInfo($"Wine / Proton Environment Detected! (Wine {WineEnv.WineVersion ?? "Unknown Version"})。Compatibility Features Enabled: Embedded Fonts / In-App Notifications / In-App About Page.");
                }

                try
                {
                    string? appVersion = null;
                    try
                    {
                        appVersion = _appConfigManager?.Get("Config", "Version");
                    }
                    catch (Exception configEx)
                    {
                        Logger.LogWarning($"AppConfigManager not available: {configEx.Message}");
                    }

                    if (!string.IsNullOrEmpty(appVersion))
                    {
                        Logger.LogInfo($"Application Version: {appVersion}");
                    }
                    else
                    {
                        Logger.LogInfo("Application Version: Not available");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Could not read application version: {ex.Message}");
                }

                Logger.LogInfo($"Build with .NET {Environment.Version}");
                Logger.LogInfo($"Running From: {Process.GetCurrentProcess().MainModule?.FileName ?? "Unknown"}");
                Logger.LogInfo($"Content Root: {_contentRootPath}");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        Logger.LogInfo("\t");
                        string unityPlayerPath = Path.Combine(_contentRootPath, "UnityPlayer.dll");

                        if (File.Exists(unityPlayerPath))
                        {
                            using var sha1 = SHA1.Create();
                            using var md5 = MD5.Create();
                            using var stream = File.OpenRead(unityPlayerPath);
                            var sha1Hash = BitConverter.ToString(sha1.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                            stream.Position = 0;
                            var md5Hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();

                            if (sha1Hash == "f533ffe6a197876244aed60fe1c2070def962c73" && md5Hash == "3efb0fce3c5c6b33d399172b6d366596")
                            {
                                Logger.LogWarning("! Detected vulnerable UnityPlayer.dll version (CVE-2025-59489). Please update to a patched version.");
                                Logger.LogWarning($"! UnityPlayer.dll SHA1: {sha1Hash}");
                                Logger.LogWarning($"! UnityPlayer.dll MD5: {md5Hash}");
                                Logger.LogWarning(" You can download the patcher(version 1.2.0) from the link below:");
                                Logger.LogWarning(" \t `https://security-patches.unity.com/bc0977e0-21a9-4f6e-9414-4f44b242110a/unity-patcher/UnityApplicationPatcher-1.2.0-Win.zip` ");
                                Logger.LogInfo("\t");
                            }
                            else
                            {
                                Logger.LogInfo($"UnityPlayer.dll hash check passed (SHA1: {sha1Hash}, MD5: {md5Hash})");
                            }
                        }
                        else
                        {
                            Logger.LogInfo("UnityPlayer.dll not found at expected path, skipping vulnerability check");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error computing UnityPlayer.dll hashes: {ex.Message}");
                    }
                }
                else
                {
                    Logger.LogInfo($"Running on {RuntimeInformation.OSDescription}, skipping UnityPlayer.dll hash check");
                }

                Logger.LogInfo("\t");
                Logger.LogInfo("========== Hardware Info ==========");
                Logger.LogInfo($"CPU: {GetProcessorInfo()} ({Environment.ProcessorCount} cores)");
                Logger.LogInfo($"RAM: {GetMemoryInfo()}GB");
                Logger.LogInfo($"OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");
                Logger.LogInfo($"Machine Name: {Environment.MachineName}");
                Logger.LogInfo($"User Name: {Environment.UserName}");
                Logger.LogInfo("===================================");
                Logger.LogInfo("\t");

                Logger.LogInfo("========== Configuration Info ==========");
                try
                {
                    var configSections = _appConfigManager?.GetAllSections() ?? new List<string>();
                    Logger.LogInfo($"Configuration sections: {configSections.Count}");
                    foreach (var section in configSections.Take(5))
                    {
                        try
                        {
                            var keys = _appConfigManager?.GetSectionKeys(section) ?? new List<string>();
                            Logger.LogInfo($"  [{section}]: {keys.Count} keys");
                        }
                        catch (Exception sectionEx)
                        {
                            Logger.LogInfo($"  [{section}]: Error reading section - {sectionEx.Message}");
                        }
                    }
                    if (configSections.Count > 5)
                    {
                        Logger.LogInfo($"  ... and {configSections.Count - 5} more sections");
                    }
                }
                catch (Exception configEx)
                {
                    Logger.LogInfo($"Configuration Info: Error reading configuration - {configEx.Message}");
                }
                Logger.LogInfo("=======================================");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error during application startup logging: {ex.Message}");
            }
        }

        public void LogApplicationShutdown()
        {
            try
            {
                Logger.LogInfo("\t");
                Logger.LogInfo("*** Application Shutting Down ***");
                Logger.LogInfo($"Shutdown Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Logger.LogInfo($"Uptime: {GetUptime()}");
                Logger.LogInfo("\t");
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"Error during application shutdown logging: {ex.Message}");
            }
        }

        /// <summary>
        /// CPU 型号。已移除 wmic（WMI COM）调用 —— wmic 在 Win11 24H2+ 被微软移除，
        /// 且 Wine/Proton 前缀中不存在。改为读取注册表 CentralProcessor 键：
        /// 该键由原生 Windows 与 Wine/Proton 共同提供，两种环境共用同一实现。
        /// </summary>
        private string GetProcessorInfo()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var cpuName = Microsoft.Win32.Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                        "ProcessorNameString", null) as string;
                    if (!string.IsNullOrWhiteSpace(cpuName))
                        return cpuName.Trim();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    foreach (var line in File.ReadLines("/proc/cpuinfo"))
                    {
                        if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                        {
                            var idx = line.IndexOf(':');
                            if (idx >= 0 && idx + 1 < line.Length)
                                return line[(idx + 1)..].Trim();
                        }
                    }
                }
            }
            catch
            {
                // 落入默认值
            }

            return $"{RuntimeInformation.ProcessArchitecture} Processor";
        }

        /// <summary>
        /// 物理内存（GB）。已移除 wmic（WMI COM）调用，改用 kernel32!GlobalMemoryStatusEx
        /// flat API —— Wine/Proton 完整实现该导出，与原生 Windows 共用同一实现。
        /// </summary>
        private double GetMemoryInfo()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                    if (GlobalMemoryStatusEx(ref status) && status.ullTotalPhys > 0)
                        return Math.Round(status.ullTotalPhys / (1024.0 * 1024.0 * 1024.0), 2);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    foreach (var line in File.ReadLines("/proc/meminfo"))
                    {
                        if (line.StartsWith("MemTotal", StringComparison.OrdinalIgnoreCase))
                        {
                            var digits = new string(line.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
                            if (long.TryParse(digits, out var totalKb) && totalKb > 0)
                                return Math.Round(totalKb / (1024.0 * 1024.0), 2);
                        }
                    }
                }
            }
            catch
            {
                // 落入默认值
            }

            return Math.Round(Environment.WorkingSet / (1024.0 * 1024.0 * 1024.0), 2);
        }

        private string GetUptime()
        {
            try
            {
                var uptime = DateTime.Now - Process.GetCurrentProcess().StartTime;
                return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
            }
            catch
            {
                return "Unknown";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    }
}
