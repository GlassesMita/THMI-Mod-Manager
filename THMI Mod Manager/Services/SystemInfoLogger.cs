using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace THMI_Mod_Manager.Services
{
    public class SystemInfoLogger
    {
        private readonly ILogger<SystemInfoLogger> _logger;
        private readonly AppConfigManager _appConfigManager;
        private readonly string _contentRootPath;

        public SystemInfoLogger(ILogger<SystemInfoLogger> logger, AppConfigManager appConfigManager, string contentRootPath)
        {
            _logger = logger;
            _appConfigManager = appConfigManager;
            _contentRootPath = contentRootPath;
        }

        public void LogApplicationStartup()
        {
            try
            {
                // Log application initialization header
                Logger.Log("*** Initialized Application ***");
                _logger.LogInformation("*** Initialized Application ***");

                // Read application version from AppConfig.Schale
                try
                {
                    string? appVersion = null;
                    try
                    {
                        appVersion = _appConfigManager?.Get("Config", "Version");
                    }
                    catch (Exception configEx)
                    {
                        _logger.LogWarning($"AppConfigManager not available: {configEx.Message}");
                    }
                    
                    if (!string.IsNullOrEmpty(appVersion))
                    {
                        Logger.Log($"Application Version: {appVersion}");
                        _logger.LogInformation($"Application Version: {appVersion}");
                    }
                    else
                    {
                        Logger.Log("Application Version: Not available");
                        _logger.LogInformation("Application Version: Not available");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(Logger.LogLevel.Warning, $"Could not read application version: {ex.Message}");
                    _logger.LogWarning($"Could not read application version: {ex.Message}");
                }

                // Log runtime information
                Logger.Log($"Build with .NET {Environment.Version}");
                Logger.Log($"Running From: {Process.GetCurrentProcess().MainModule?.FileName ?? "Unknown"}");
                Logger.Log($"Content Root: {_contentRootPath}");
                
                _logger.LogInformation($"Build with .NET {Environment.Version}");
                _logger.LogInformation($"Running From: {Process.GetCurrentProcess().MainModule?.FileName ?? "Unknown"}");
                _logger.LogInformation($"Content Root: {_contentRootPath}");

                // UnityPlayer.dll vulnerability detection (Windows only)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        Logger.Log("\t");
                        string unityPlayerPath = Path.Combine(_contentRootPath, "UnityPlayer.dll");
                        
                        if (File.Exists(unityPlayerPath))
                        {
                            using var sha1 = SHA1.Create();
                            using var md5 = MD5.Create();
                            using var stream = File.OpenRead(unityPlayerPath);
                            var sha1Hash = BitConverter.ToString(sha1.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                            stream.Position = 0; // Reset stream position for MD5 calculation
                            var md5Hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();

                            // Check for CVE-2025-59489 vulnerability
                            if (sha1Hash == "f533ffe6a197876244aed60fe1c2070def962c73" && md5Hash == "3efb0fce3c5c6b33d399172b6d366596")
                            {
                                Logger.Log(Logger.LogLevel.Warning, "! Detected vulnerable UnityPlayer.dll version (CVE-2025-59489). Please update to a patched version.");
                                Logger.Log(Logger.LogLevel.Warning, $"! UnityPlayer.dll SHA1: {sha1Hash}");
                                Logger.Log(Logger.LogLevel.Warning, $"! UnityPlayer.dll MD5: {md5Hash}");
                                Logger.Log(Logger.LogLevel.Warning, " You can download the patcher(version 1.2.0) from the link below:");
                                Logger.Log(Logger.LogLevel.Warning, " \t `https://security-patches.unity.com/bc0977e0-21a9-4f6e-9414-4f44b242110a/unity-patcher/UnityApplicationPatcher-1.2.0-Win.zip` ");
                                Logger.Log("\t");

                                _logger.LogWarning("Detected vulnerable UnityPlayer.dll version (CVE-2025-59489). Please update to a patched version.");
                                _logger.LogWarning($"UnityPlayer.dll SHA1: {sha1Hash}");
                                _logger.LogWarning($"UnityPlayer.dll MD5: {md5Hash}");
                            }
                            else
                            {
                                Logger.Log($"UnityPlayer.dll hash check passed (SHA1: {sha1Hash}, MD5: {md5Hash})");
                                _logger.LogInformation($"UnityPlayer.dll hash check passed (SHA1: {sha1Hash}, MD5: {md5Hash})");
                            }
                        }
                        else
                        {
                            Logger.Log("UnityPlayer.dll not found at expected path, skipping vulnerability check");
                            _logger.LogInformation("UnityPlayer.dll not found at expected path, skipping vulnerability check");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(Logger.LogLevel.Error, $"Error computing UnityPlayer.dll hashes: {ex.Message}");
                        _logger.LogError($"Error computing UnityPlayer.dll hashes: {ex.Message}");
                    }
                }
                else
                {
                    Logger.Log($"Running on {RuntimeInformation.OSDescription}, skipping UnityPlayer.dll hash check");
                    _logger.LogInformation($"Running on {RuntimeInformation.OSDescription}, skipping UnityPlayer.dll hash check");
                }

                Logger.Log("\t");
                Logger.Log("========== Hardware Info ==========");
                Logger.Log($"CPU: {GetProcessorInfo()} ({Environment.ProcessorCount} cores)");
                Logger.Log($"RAM: {GetMemoryInfo()}GB");
                Logger.Log($"OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");
                Logger.Log($"Machine Name: {Environment.MachineName}");
                Logger.Log($"User Name: {Environment.UserName}");
                Logger.Log("===================================");
                Logger.Log("\t");

                _logger.LogInformation("========== Hardware Info ==========");
                _logger.LogInformation($"CPU: {GetProcessorInfo()} ({Environment.ProcessorCount} cores)");
                _logger.LogInformation($"RAM: {GetMemoryInfo()}GB");
                _logger.LogInformation($"OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");
                _logger.LogInformation($"Machine Name: {Environment.MachineName}");
                _logger.LogInformation($"User Name: {Environment.UserName}");
                _logger.LogInformation("===================================");

                // Log configuration information
                Logger.Log("\t");
                Logger.Log("========== Configuration Info ==========");
                try
                {
                    var configSections = _appConfigManager?.GetAllSections() ?? new List<string>();
                    Logger.Log($"Configuration sections: {configSections.Count}");
                    foreach (var section in configSections.Take(5)) // Log first 5 sections
                    {
                        try
                        {
                            var keys = _appConfigManager?.GetSectionKeys(section) ?? new List<string>();
                            Logger.Log($"  [{section}]: {keys.Count} keys");
                        }
                        catch (Exception sectionEx)
                        {
                            Logger.Log($"  [{section}]: Error reading section - {sectionEx.Message}");
                        }
                    }
                    if (configSections.Count > 5)
                    {
                        Logger.Log($"  ... and {configSections.Count - 5} more sections");
                    }
                }
                catch (Exception configEx)
                {
                    Logger.Log($"Configuration Info: Error reading configuration - {configEx.Message}");
                }
                Logger.Log("=======================================");
                Logger.Log("\t");

                _logger.LogInformation("========== Configuration Info ==========");
                try
                {
                    var configSections = _appConfigManager?.GetAllSections() ?? new List<string>();
                    _logger.LogInformation($"Configuration sections: {configSections.Count}");
                }
                catch (Exception configEx)
                {
                    _logger.LogInformation($"Configuration Info: Error reading configuration - {configEx.Message}");
                }
                _logger.LogInformation("=======================================");
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.LogLevel.Error, $"Error during application startup logging: {ex.Message}");
                _logger.LogError($"Error during application startup logging: {ex.Message}");
            }
        }

        public void LogApplicationShutdown()
        {
            try
            {
                Logger.Log("\t");
                Logger.Log("*** Application Shutting Down ***");
                Logger.Log($"Shutdown Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Logger.Log($"Uptime: {GetUptime()}");
                Logger.Log("\t");

                _logger.LogInformation("*** Application Shutting Down ***");
                _logger.LogInformation($"Shutdown Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                _logger.LogInformation($"Uptime: {GetUptime()}");
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.LogLevel.Error, $"Error during application shutdown logging: {ex.Message}");
                _logger.LogError($"Error during application shutdown logging: {ex.Message}");
            }
        }

        private string GetProcessorInfo()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "wmic",
                        Arguments = "cpu get name",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });
                    
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        
                        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 1)
                        {
                            return lines[1].Trim();
                        }
                    }
                }
                
                return $"{RuntimeInformation.ProcessArchitecture} Processor";
            }
            catch
            {
                return $"{RuntimeInformation.ProcessArchitecture} Processor";
            }
        }

        private double GetMemoryInfo()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "wmic",
                        Arguments = "computersystem get totalphysicalmemory",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });
                    
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        
                        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 1 && long.TryParse(lines[1].Trim(), out long totalMemory))
                        {
                            return Math.Round(totalMemory / (1024.0 * 1024.0 * 1024.0), 2);
                        }
                    }
                }
                
                return Math.Round(Environment.WorkingSet / (1024.0 * 1024.0 * 1024.0), 2);
            }
            catch
            {
                return Math.Round(Environment.WorkingSet / (1024.0 * 1024.0 * 1024.0), 2);
            }
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
    }
}