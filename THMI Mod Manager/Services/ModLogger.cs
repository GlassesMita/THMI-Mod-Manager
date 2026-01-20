using System;
using System.IO;

namespace THMI_Mod_Manager.Services
{
    /// <summary>
    /// Mod专用日志记录器
    /// Mod只能调用此类的LogMod方法，确保Mod无法访问其他日志功能
    /// </summary>
    public static class ModLogger
    {
        private static string? logFilePath;
        private static readonly object lockObject = new object();

        static ModLogger()
        {
            InitializeLogFilePath();
        }

        private static void InitializeLogFilePath()
        {
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string logDirectory = Path.Combine(baseDirectory, "Logs");
                
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                logFilePath = Path.Combine(logDirectory, "Mod.Log");
                
                if (File.Exists(logFilePath))
                {
                    File.WriteAllText(logFilePath, string.Empty);
                }
                else
                {
                    File.Create(logFilePath).Dispose();
                }
                
                LogMod("ModLogger initialized", Logger.LogLevel.Info);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize ModLogger: {ex.Message}");
                logFilePath = null;
            }
        }

        /// <summary>
        /// Mod专用的日志记录方法
        /// 输出格式: [M][DateTime][Level]
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="level">日志级别 (Info/Warning/Error)</param>
        public static void LogMod(string message, Logger.LogLevel level = Logger.LogLevel.Info)
        {
            if (string.IsNullOrEmpty(logFilePath))
            {
                InitializeLogFilePath();
            }

            string levelTag = GetLevelShortTag(level);
            string logMessage = $"[M][{DateTime.Now:yyyy/MM/dd HH:mm:ss.ffff}][{levelTag}] {message}";
            
            lock (lockObject)
            {
                try
                {
                    if (!string.IsNullOrEmpty(logFilePath))
                    {
                        File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write to Mod log file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Mod专用的格式化日志记录方法
        /// </summary>
        public static void LogMod(string format, Logger.LogLevel level, params object[] args)
        {
            string message = args != null && args.Length > 0 ? string.Format(format, args) : format;
            LogMod(message, level);
        }

        private static string GetLevelShortTag(Logger.LogLevel level)
        {
            switch (level)
            {
                case Logger.LogLevel.Warning: return "W";
                case Logger.LogLevel.Error: return "E";
                case Logger.LogLevel.Info:
                default:
                    return "I";
            }
        }

        public static string? GetLogFilePath()
        {
            return logFilePath;
        }
    }

    /// <summary>
    /// Mod访问控制特性
    /// 用于标记哪些方法可以被Mod调用
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ModAccessAttribute : Attribute
    {
        public string Description { get; set; } = "";
        
        public ModAccessAttribute()
        {
        }
        
        public ModAccessAttribute(string description)
        {
            Description = description;
        }
    }
}
