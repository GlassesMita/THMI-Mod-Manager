using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace THMI_Mod_Manager.Services
{
    /// <summary>
    /// Logging service for application-wide logging / 应用程序范围的日志服务
    /// Supports multiple log levels and browser notifications / 支持多个日志级别和浏览器通知
    /// </summary>
    public class Logger
    {
        /// <summary>
        /// Log level enumeration / 日志级别枚举
        /// </summary>
        public enum LogLevel
        {
            /// <summary>Information / 信息</summary>
            Info,
            /// <summary>Warning / 警告</summary>
            Warning,
            /// <summary>Error / 错误</summary>
            Error,
            /// <summary>Extra / 额外</summary>
            Ex,
            /// <summary>Mod-specific / 模组特定</summary>
            Mod
        }
        
        private static string? logFilePath;
        private static readonly object lockObject = new object();
        
        private static readonly List<BrowserNotification> notificationQueue = new List<BrowserNotification>();
        private static readonly object notificationLock = new object();
        
        private static string? _lastNotificationKey;
        private static DateTime _lastNotificationTime = DateTime.MinValue;
        
        private const int NOTIFICATION_COOLDOWN_MS = 3000;
        
        /// <summary>
        /// Static constructor / 静态构造函数
        /// Initializes the log file path on first use / 首次使用时初始化日志文件路径
        /// </summary>
        static Logger()
        {
            InitializeLogFilePath();
        }

        /// <summary>
        /// Log a message with default Info level / 使用默认Info级别记录消息
        /// </summary>
        /// <param name="message">Message to log / 要记录的消息</param>
        public static void Log(string message)
        {
            Log(message, LogLevel.Info);
        }

        /// <summary>
        /// Log an error message / 记录错误消息
        /// </summary>
        /// <param name="message">Error message to log / 要记录的错误消息</param>
        public static void LogError(string message)
        {
            Log(message, LogLevel.Error);
        }

        /// <summary>
        /// Log a warning message / 记录警告消息
        /// </summary>
        /// <param name="message">Warning message to log / 要记录的警告消息</param>
        public static void LogWarning(string message)
        {
            Log(message, LogLevel.Warning);
        }

        /// <summary>
        /// Log an info message / 记录信息消息
        /// </summary>
        /// <param name="message">Info message to log / 要记录的信息消息</param>
        public static void LogInfo(string message)
        {
            Log(message, LogLevel.Info);
        }

        /// <summary>
        /// Log an extra message / 记录额外消息
        /// </summary>
        /// <param name="message">Extra message to log / 要记录的额外消息</param>
        public static void LogEx(string message)
        {
            Log(message, LogLevel.Ex);
        }

        /// <summary>
        /// Mod-specific logging method with default Info level
        /// / 模组专用日志记录方法，默认Info级别
        /// </summary>
        /// <param name="message">Message to log / 要记录的消息</param>
        [ModAccess("Mod专用日志记录方法，支持日志级别参数")]
        public static void LogMod(string message)
        {
            LogMod(message, LogLevel.Info);
        }

        /// <summary>
        /// Mod-specific logging method with specified level
        /// / 模组专用日志记录方法，支持指定级别
        /// </summary>
        /// <param name="message">Message to log / 要记录的消息</param>
        /// <param name="level">Log level / 日志级别</param>
        [ModAccess("Mod专用日志记录方法，支持日志级别参数")]
        public static void LogMod(string message, LogLevel level)
        {
            if (string.IsNullOrEmpty(logFilePath))
            {
                InitializeLogFilePath();
            }

            string levelTag = GetLevelShortTag(level);
            string logMessage = $"[M][{DateTime.Now:yyyy/MM/dd HH:mm:ss.ffff}] [{levelTag}] {message}";
            
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
                    Console.WriteLine($"Failed to write to log file: {ex.Message}");
                }
            }
        }

        [ModAccess("Mod专用格式化日志记录方法")]
        public static void LogMod(string format, LogLevel level, params object[] args)
        {
            string message = args != null && args.Length > 0 ? string.Format(format, args) : format;
            LogMod(message, level);
        }

        public static void Log(string message, LogLevel level)
        {
            if (string.IsNullOrEmpty(logFilePath))
            {
                InitializeLogFilePath();
            }

            string tag = GetLevelShortTag(level);
            string logMessage = $"[{tag}][{DateTime.Now:yyyy/MM/dd HH:mm:ss.ffff}] {message}";
            
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
                    Console.WriteLine($"Failed to write to log file: {ex.Message}");
                }
            }
            
            Console.WriteLine(logMessage);
        }

        public static void Log(LogLevel level, string message)
        {
            Log(message, level);
        }

        public static void Log(string format, LogLevel level, params object[] args)
        {
            string message = args != null && args.Length > 0 ? string.Format(format, args) : format;
            Log(message, level);
        }

        public static void LogDebug(string message)
        {
            Log(message, LogLevel.Info);
        }

        public static void LogDebug(string format, params object[] args)
        {
            string message = args != null && args.Length > 0 ? string.Format(format, args) : format;
            LogDebug(message);
        }

        public static void LogTrace(string message)
        {
            Log(message, LogLevel.Info);
        }

        public static void LogTrace(string format, params object[] args)
        {
            string message = args != null && args.Length > 0 ? string.Format(format, args) : format;
            LogTrace(message);
        }

        public static void LogWarning(string format, params object[] args)
        {
            string message = args != null && args.Length > 0 ? string.Format(format, args) : format;
            LogWarning(message);
        }

        public static void LogWarning(Exception ex, string message)
        {
            string fullMessage = $"{message}\nException: {ex.GetType().Name}: {ex.Message}";
            if (ex.StackTrace != null)
            {
                fullMessage += $"\nStackTrace:\n{ex.StackTrace}";
            }
            Log(fullMessage, LogLevel.Warning);
        }

        public static void LogError(string format, params object[] args)
        {
            string message = args != null && args.Length > 0 ? string.Format(format, args) : format;
            LogError(message);
        }

        public static void LogError(Exception ex, string message)
        {
            string fullMessage = $"{message}\nException: {ex.GetType().Name}: {ex.Message}";
            if (ex.StackTrace != null)
            {
                fullMessage += $"\nStackTrace:\n{ex.StackTrace}";
            }
            Log(fullMessage, LogLevel.Error);
        }

        public static void LogDebug(Exception ex, string message)
        {
            string fullMessage = $"{message}\nException: {ex.GetType().Name}: {ex.Message}";
            if (ex.StackTrace != null)
            {
                fullMessage += $"\nStackTrace:\n{ex.StackTrace}";
            }
            Log(fullMessage, LogLevel.Info);
        }

        public static void LogException(string message, Exception ex)
        {
            string fullMessage = $"{message}\nException: {ex.GetType().Name}: {ex.Message}";
            if (ex.StackTrace != null)
            {
                fullMessage += $"\nStackTrace:\n{ex.StackTrace}";
            }
            Log(fullMessage, LogLevel.Ex);
        }

        public static void LogException(Exception ex, string message)
        {
            LogException(message, ex);
        }

        public static void LogException(Exception ex)
        {
            LogException("Exception occurred", ex);
        }

        public static void LogException(string format, Exception ex, params object[] args)
        {
            string message = args != null && args.Length > 0 ? string.Format(format, args) : format;
            LogException(message, ex);
        }

        private static string GetLevelShortTag(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Mod: return "M";
                case LogLevel.Ex: return "X";
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
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string logDirectory = Path.Combine(baseDirectory, "Logs");
                
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                logFilePath = Path.Combine(logDirectory, "Latest.Log");
                
                if (File.Exists(logFilePath))
                {
                    File.WriteAllText(logFilePath, string.Empty);
                }
                else
                {
                    File.Create(logFilePath).Dispose();
                }

                // ===================== AI Log Analysis Prompt =====================
                Log("================================================================================");
                Log("[AI PROMPT] THMI Mod Manager Log Analysis Guide");
                Log("================================================================================");
                Log("EN: When analyzing this log, consider: error analysis, performance issues, flow analysis, config issues, warnings, mod management, network calls");
                Log("CN: 分析此日志时请考虑：错误分析、性能问题、流程分析、配置问题、警告、Mod管理、网络调用");
                Log("================================================================================");
                Log("\t");
                
                Log("Logger initialized successfully", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize logger: {ex.Message}");
                Logger.LogError($"Failed to initialize logger: {ex.Message}");
                logFilePath = null;
            }
        }

        public static string? GetLogFilePath()
        {
            return logFilePath;
        }

        public static string? GetLogDirectory()
        {
            if (string.IsNullOrEmpty(logFilePath))
                return null;
            return Path.GetDirectoryName(logFilePath);
        }
        
        public static void SendBrowserNotification(string title, string message, string type = "warning")
        {
            var notification = new BrowserNotification
            {
                Id = Guid.NewGuid().ToString("N")[..16],
                Title = title,
                Message = message,
                Type = type,
                Timestamp = DateTime.Now.ToString("o"),
                Key = $"{title}:{message}"
            };
            
            lock (notificationLock)
            {
                if (ShouldSkipNotification(notification.Key))
                {
                    return;
                }
                
                notificationQueue.Add(notification);
                _lastNotificationKey = notification.Key;
                _lastNotificationTime = DateTime.Now;
            }
        }
        
        private static bool ShouldSkipNotification(string key)
        {
            if (_lastNotificationKey == key)
            {
                var elapsed = DateTime.Now - _lastNotificationTime;
                if (elapsed.TotalMilliseconds < NOTIFICATION_COOLDOWN_MS)
                {
                    return true;
                }
            }
            return false;
        }
        
        public static List<BrowserNotification> GetPendingNotifications()
        {
            lock (notificationLock)
            {
                var pending = new List<BrowserNotification>(notificationQueue);
                notificationQueue.Clear();
                return pending;
            }
        }
        
        public static void ClearNotifications()
        {
            lock (notificationLock)
            {
                notificationQueue.Clear();
            }
        }
    }

    public class BrowserNotification
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Type { get; set; } = "info";
        public string Timestamp { get; set; } = "";
        public string Key { get; set; } = "";
    }
}