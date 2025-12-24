using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace THMI_Mod_Manager.Services
{
    public class Logger
    {
        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }
        
        private static string? logFilePath;
        private static readonly object lockObject = new object();

        // 静态构造函数，确保在类加载时初始化 logFilePath
        static Logger()
        {
            InitializeLogFilePath();
        }

        public static void Log(string message)
        {
            // 兼容旧版，仅传 message 时视为 Info
            Log(message, LogLevel.Info);
        }

        public static void LogError(string message)
        {
            Log(message, LogLevel.Error);
        }

        public static void LogWarning(string message)
        {
            Log(message, LogLevel.Warning);
        }

        public static void LogInfo(string message)
        {
            Log(message, LogLevel.Info);
        }

        // 写入消息并指定等级（不使用默认参数，以免与 Log(string) 冲突）
        public static void Log(string message, LogLevel level)
        {
            if (string.IsNullOrEmpty(logFilePath))
            {
                InitializeLogFilePath();
            }

            string tag = GetLevelShortTag(level);
            string logMessage = $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}][{tag}] {message}";
            
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
                    // 如果写入失败，可以尝试写入到备用位置或忽略
                    Console.WriteLine($"Failed to write to log file: {ex.Message}");
                }
            }
        }

        // 显式传入 (LogLevel, message) 的重载，方便调用方以不同顺序传参
        public static void Log(LogLevel level, string message)
        {
            Log(message, level);
        }

        // 支持格式化字符串并指定等级
        public static void Log(string format, LogLevel level, params object[] args)
        {
            string message = args != null && args.Length > 0 ? string.Format(format, args) : format;
            Log(message, level);
        }

        private static string GetLevelShortTag(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Warning: return "W";
                case LogLevel.Error: return "E";
                case LogLevel.Info:
                default:
                    return "I";
            }
        }

        private static void InitializeLogFilePath()
        {
            try
            {
                // 获取应用程序的根目录
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                
                // 创建日志目录路径
                string logDirectory = Path.Combine(baseDirectory, "Logs");
                
                // 确保日志目录存在
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // 设置日志文件路径
                logFilePath = Path.Combine(logDirectory, "Latest.Log");
                
                // 检查日志文件是否存在
                if (File.Exists(logFilePath))
                {
                    // 清空文件内容
                    File.WriteAllText(logFilePath, string.Empty);
                }
                else
                {
                    // 创建日志文件
                    File.Create(logFilePath).Dispose();
                }
                
                // 记录日志系统启动
                Log("Logger initialized successfully", LogLevel.Info);
            }
            catch (Exception ex)
            {
                // 如果初始化失败，记录到控制台
                Console.WriteLine($"Failed to initialize logger: {ex.Message}");
                logFilePath = null;
            }
        }

        // 获取当前日志文件路径
        public static string? GetLogFilePath()
        {
            return logFilePath;
        }

        // 获取日志目录路径
        public static string? GetLogDirectory()
        {
            if (string.IsNullOrEmpty(logFilePath))
                return null;
            return Path.GetDirectoryName(logFilePath);
        }
    }
}