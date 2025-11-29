using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager.Controllers
{
    [Route("api/logger")]
    [ApiController]
    public class LoggerController : ControllerBase
    {
        // GET: api/logger/status
        [HttpGet("status")]
        public IActionResult GetLoggerStatus()
        {
            try
            {
                string logPath = Logger.GetLogFilePath();
                bool isActive = !string.IsNullOrEmpty(logPath) && System.IO.File.Exists(logPath);
                
                return Ok(new 
                { 
                    isActive = isActive, 
                    logPath = logPath,
                    logDirectory = Logger.GetLogDirectory()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: api/logger/test?level=info&message=test
        [HttpGet("test")]
        public IActionResult TestLogger([FromQuery] string level, [FromQuery] string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                {
                    return BadRequest(new { error = "Message parameter is required" });
                }

                // 解析日志级别
                Logger.LogLevel logLevel;
                switch (level?.ToLower())
                {
                    case "warning":
                    case "warn":
                        logLevel = Logger.LogLevel.Warning;
                        Logger.LogWarning($"[TEST] {message}");
                        break;
                    case "error":
                    case "err":
                        logLevel = Logger.LogLevel.Error;
                        Logger.LogError($"[TEST] {message}");
                        break;
                    case "info":
                    case "information":
                    default:
                        logLevel = Logger.LogLevel.Info;
                        Logger.LogInfo($"[TEST] {message}");
                        break;
                }

                return Ok(new 
                { 
                    success = true, 
                    message = $"Log written successfully at {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    level = logLevel.ToString(),
                    content = message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: api/logger/logs
        [HttpGet("logs")]
        public IActionResult GetLogs([FromQuery] int lines = 100)
        {
            try
            {
                string logPath = Logger.GetLogFilePath();
                
                if (string.IsNullOrEmpty(logPath) || !System.IO.File.Exists(logPath))
                {
                    return NotFound(new { error = "Log file not found" });
                }

                // 读取日志文件的最后几行
                var logLines = new List<string>();
                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    string line;
                    var allLines = new List<string>();
                    while ((line = sr.ReadLine()) != null)
                    {
                        allLines.Add(line);
                    }
                    
                    // 获取最后几行
                    int startIndex = Math.Max(0, allLines.Count - lines);
                    logLines = allLines.GetRange(startIndex, allLines.Count - startIndex);
                }

                return Ok(new 
                { 
                    logPath = logPath,
                    totalLines = logLines.Count,
                    logs = logLines
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // DELETE: api/logger/clear
        [HttpDelete("clear")]
        public IActionResult ClearLogs()
        {
            try
            {
                string logPath = Logger.GetLogFilePath();
                
                if (string.IsNullOrEmpty(logPath))
                {
                    return NotFound(new { error = "Log path not configured" });
                }

                // 清空日志文件
                if (System.IO.File.Exists(logPath))
                {
                    System.IO.File.WriteAllText(logPath, string.Empty);
                    Logger.LogInfo("Log file cleared by user request");
                }

                return Ok(new { success = true, message = "Log file cleared successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST: api/logger/log
        [HttpPost("log")]
        public IActionResult WriteLog([FromBody] LogRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Message))
                {
                    return BadRequest(new { error = "Message is required" });
                }

                // 解析日志级别
                Logger.LogLevel logLevel;
                switch (request.Level?.ToLower())
                {
                    case "warning":
                    case "warn":
                        logLevel = Logger.LogLevel.Warning;
                        Logger.LogWarning(request.Message);
                        break;
                    case "error":
                    case "err":
                        logLevel = Logger.LogLevel.Error;
                        Logger.LogError(request.Message);
                        break;
                    case "info":
                    case "information":
                    default:
                        logLevel = Logger.LogLevel.Info;
                        Logger.LogInfo(request.Message);
                        break;
                }

                return Ok(new 
                { 
                    success = true, 
                    message = "Log written successfully",
                    level = logLevel.ToString(),
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    // 日志请求模型
    public class LogRequest
    {
        public string Message { get; set; }
        public string Level { get; set; } = "info";
    }
}