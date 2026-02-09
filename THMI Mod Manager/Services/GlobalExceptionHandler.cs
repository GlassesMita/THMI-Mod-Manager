using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace THMI_Mod_Manager.Services
{
    /// <summary>
    /// 全局异常处理器 - 显示 Linux Kernel Panic 风格的错误界面
    /// </summary>
    public static class GlobalExceptionHandler
    {
        private static readonly string KernelPanicAscii = @"
      .--.        _ 
     |o_o |      | | 
     |:_/ |      | | 
    //   \ \     |_| 
   (|     | )     _ 
  /'\_   _/`\    (_) 
  \___)=(___/ 
";

        private static readonly string[] OopsAscii = new string[]
        {
            @"  _____                                _     _                  ",
            @" | ____| __  __   ___    ___   _ __   | |_  (_)   ___    _ __   ",
            @" |  _|   \ \/ /  / __|  / _ \ | '_ \  | __| | |  / _ \  | '_ \ ",
            @" | |___   >  <  | (__  |  __/ | |_) | | |_  | | | (_) | | | | | ",
            @" |_____| /_/\_\  \___|  \___| | .__/   \__| |_|  \___/  |_| |_| ",
            @"                              |_|                               ",
        };

        public static void Initialize()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            
            Console.CancelKeyPress += OnCancelKeyPress;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            DisplayKernelPanic(exception ?? new Exception("Unknown error"));
            
            // 确保缓冲区刷新
            Console.Out.Flush();
            Console.Error.Flush();
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            Console.Out.Flush();
        }

        private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("\n\nReceived cancel signal. Exiting gracefully...\n");
            Console.Out.Flush();
        }

        public static void DisplayKernelPanic(Exception exception)
        {
            DisplayKernelPanicWithUI(exception, waitForKey: true);
        }

        /// <summary>
        /// 显示内核恐慌界面（用于实际未处理异常）
        /// </summary>
        public static void DisplayKernelPanicWithUI(Exception exception, bool waitForKey = true)
        {
            // 清屏
            Console.Clear();
            
            // 输出 Kernel Panic ASCII
            Console.ForegroundColor = ConsoleColor.Red;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.WriteLine(KernelPanicAscii);
            Console.ResetColor();
            
            Console.WriteLine();
            
            // 输出 Oops 标题
            foreach (var line in OopsAscii)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(line);
            }
            
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(new string('=', 78));
            Console.WriteLine();
            
            // 输出异常信息
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss.ffff}]");
            Console.ResetColor();
            Console.WriteLine();
            
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("KERNEL PANIC - UNHANDLED EXCEPTION");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(new string('-', 78));
            Console.WriteLine();
            
            // 输出异常类型和消息
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Exception Type: {exception.GetType().FullName}");
            Console.ResetColor();
            Console.WriteLine();
            
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Message:");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(exception.Message);
            Console.ResetColor();
            Console.WriteLine();
            
            // 输出堆栈跟踪
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Stack Trace:");
                Console.ForegroundColor = ConsoleColor.Gray;
                
                var stackTrace = exception.StackTrace.Split('\n');
                foreach (var line in stackTrace)
                {
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        Console.WriteLine($"  {trimmedLine}");
                    }
                }
                Console.ResetColor();
                Console.WriteLine();
            }
            
            // 输出内部异常
            if (exception.InnerException != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Inner Exception:");
                Console.ResetColor();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  Type: {exception.InnerException.GetType().FullName}");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"  Message: {exception.InnerException.Message}");
                Console.ResetColor();
                Console.WriteLine();
            }
            
            Console.WriteLine(new string('-', 78));
            Console.WriteLine();
            
            // 输出系统信息
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("System Information:");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"  OS: {RuntimeInformation.OSDescription}");
            Console.WriteLine($"  Architecture: {RuntimeInformation.OSArchitecture}");
            Console.WriteLine($"  .NET Version: {Environment.Version}");
            Console.WriteLine($"  Process ID: {Environment.ProcessId}");
            Console.WriteLine($"  Working Directory: {Environment.CurrentDirectory}");
            Console.WriteLine();
            
            Console.WriteLine(new string('=', 78));
            Console.WriteLine();
            
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("THE SYSTEM HAS ENCOUNTERED AN UNRECOVERABLE ERROR.");
            Console.WriteLine("PLEASE RESTART THE APPLICATION.");
            Console.ResetColor();
            Console.WriteLine();
            
            // 记录到日志文件
            WriteToLogFile(exception);
            
            if (waitForKey)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ResetColor();
                
                // 等待用户按键
                try
                {
                    Console.ReadKey(true);
                }
                catch
                {
                    // 忽略读取错误
                }
            }
        }

        /// <summary>
        /// 记录内核恐慌日志（用于测试，不显示 UI）
        /// 适用于从 HTTP 处理程序调用，避免干扰请求响应
        /// </summary>
        public static void LogKernelPanic(Exception exception)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                           KERNEL PANIC TRIGGERED                                 ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss.ffff}]");
            sb.AppendLine();
            sb.AppendLine("Exception Type: " + exception.GetType().FullName);
            sb.AppendLine();
            sb.AppendLine("Message:");
            sb.AppendLine(exception.Message);
            sb.AppendLine();
            
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(exception.StackTrace);
                sb.AppendLine();
            }
            
            if (exception.InnerException != null)
            {
                sb.AppendLine("Inner Exception:");
                sb.AppendLine($"  Type: {exception.InnerException.GetType().FullName}");
                sb.AppendLine($"  Message: {exception.InnerException.Message}");
                sb.AppendLine();
            }
            
            sb.AppendLine("System Information:");
            sb.AppendLine($"  OS: {RuntimeInformation.OSDescription}");
            sb.AppendLine($"  Architecture: {RuntimeInformation.OSArchitecture}");
            sb.AppendLine($"  .NET Version: {Environment.Version}");
            sb.AppendLine($"  Process ID: {Environment.ProcessId}");
            sb.AppendLine($"  Working Directory: {Environment.CurrentDirectory}");
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("NOTE: This is a simulated kernel panic for testing purposes.");
            sb.AppendLine("      The actual kernel panic UI would be shown on unhandled exceptions.");
            sb.AppendLine();
            
            // 同时输出到控制台和日志文件
            var message = sb.ToString();
            Console.WriteLine(message);
            WriteToLogFile(exception);
        }

        private static void WriteToLogFile(Exception exception)
        {
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string logDirectory = Path.Combine(baseDirectory, "Logs");
                
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
                
                string logFilePath = Path.Combine(logDirectory, $"KernelPanic_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                
                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine($"KERNEL PANIC LOG - {DateTime.Now:yyyy/MM/dd HH:mm:ss.ffff}");
                sb.AppendLine("================================================================================");
                sb.AppendLine();
                sb.AppendLine("Exception Information:");
                sb.AppendLine($"Type: {exception.GetType().FullName}");
                sb.AppendLine($"Message: {exception.Message}");
                sb.AppendLine();
                
                if (!string.IsNullOrEmpty(exception.StackTrace))
                {
                    sb.AppendLine("Stack Trace:");
                    sb.AppendLine(exception.StackTrace);
                    sb.AppendLine();
                }
                
                if (exception.InnerException != null)
                {
                    sb.AppendLine("Inner Exception:");
                    sb.AppendLine($"Type: {exception.InnerException.GetType().FullName}");
                    sb.AppendLine($"Message: {exception.InnerException.Message}");
                    sb.AppendLine();
                }
                
                sb.AppendLine("System Information:");
                sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
                sb.AppendLine($"Architecture: {RuntimeInformation.OSArchitecture}");
                sb.AppendLine($".NET Version: {Environment.Version}");
                sb.AppendLine($"Process ID: {Environment.ProcessId}");
                sb.AppendLine($"Working Directory: {Environment.CurrentDirectory}");
                sb.AppendLine();
                sb.AppendLine("================================================================================");
                
                File.WriteAllText(logFilePath, sb.ToString());
            }
            catch
            {
                // 忽略日志写入错误
            }
        }
    }
}
