using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace THMI_Mod_Manager.Controllers
{
    [ApiController]
    [Route("api/filebrowser")]
    public class FileBrowserController : ControllerBase
    {
        [HttpGet("appdirectory")]
        public IActionResult GetAppDirectory()
        {
            try
            {
                // 获取应用运行目录
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                return Ok(new { success = true, path = appDirectory });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
        
        [HttpGet("drives")]
        public IActionResult GetDrives()
        {
            try
            {
                // 获取所有可用驱动器
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady)
                    .Select(d => new {
                        name = d.Name,
                        volumeLabel = d.VolumeLabel,
                        driveType = d.DriveType.ToString(),
                        fileSystem = d.DriveFormat,
                        totalSize = d.TotalSize,
                        availableFreeSpace = d.AvailableFreeSpace
                    })
                    .ToList();
                
                return Ok(new { success = true, drives = drives });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
        
        [HttpGet("list")]
        public IActionResult ListFiles([FromQuery] string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                {
                    return BadRequest(new { success = false, message = "Invalid directory path" });
                }
                
                // 获取父目录
                string? parentDirectory = null;
                try
                {
                    DirectoryInfo? dirInfo = Directory.GetParent(path);
                    parentDirectory = dirInfo?.FullName;
                }
                catch { /* 忽略无法获取父目录的情况 */ }
                
                // 获取目录列表
                var directories = Directory.GetDirectories(path)
                    .Where(dir => !string.IsNullOrEmpty(dir))
                    .Select(dir => new { 
                        name = Path.GetFileName(dir),
                        path = dir
                    })
                    .ToList();
                
                // 获取文件列表
                var files = Directory.GetFiles(path)
                    .Select(file => new { 
                        name = Path.GetFileName(file),
                        path = file,
                        extension = Path.GetExtension(file)
                    })
                    .ToList();
                
                return Ok(new {
                    success = true,
                    parentDirectory = parentDirectory,
                    directories = directories,
                    files = files
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}