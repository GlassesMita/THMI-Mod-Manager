using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Runtime.InteropServices;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FolderController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_SHOW = 5;

        public FolderController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("open-save-folder")]
        public IActionResult OpenSaveFolder()
        {
            try
            {
                var saveFolderPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
                    "Epicomic",
                    "Touhou Mystia Izakaya",
                    "Memory",
                    "Save",
                    "BetaV9"
                );
                Logger.LogInfo($"Save folder path: {saveFolderPath}");

                if (!Directory.Exists(saveFolderPath))
                {
                    Logger.LogWarning($"Save folder does not exist: {saveFolderPath}");
                    return NotFound(new { message = "Save folder not found", path = saveFolderPath });
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = saveFolderPath,
                    UseShellExecute = true
                });

                BringWindowToFront("Save Folder");

                return Ok(new { message = "Save folder opened", path = saveFolderPath });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error opening save folder");
                return StatusCode(500, new { message = "Error opening save folder", error = ex.Message });
            }
        }

        [HttpPost("open-bepinex-log-folder")]
        public IActionResult OpenBepInExLogFolder()
        {
            try
            {
                var gamePath = AppContext.BaseDirectory;
                Logger.LogInfo($"BepInEx log folder using application running directory: {gamePath}");

                var logFolderPath = Path.Combine(gamePath, "BepInEx");
                Logger.LogInfo($"BepInEx log folder path: {logFolderPath}");

                if (!Directory.Exists(logFolderPath))
                {
                    Logger.LogWarning($"BepInEx log folder does not exist: {logFolderPath}");
                    return NotFound(new { message = "BepInEx log folder not found. Expected path: " + logFolderPath, path = logFolderPath });
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = logFolderPath,
                    UseShellExecute = true
                });

                BringWindowToFront("BepInEx");

                return Ok(new { message = "BepInEx log folder opened", path = logFolderPath });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error opening BepInEx log folder");
                return StatusCode(500, new { message = "Error opening BepInEx log folder", error = ex.Message });
            }
        }

        [HttpPost("open-app-folder")]
        public IActionResult OpenAppFolder()
        {
            try
            {
                var appPath = AppContext.BaseDirectory;

                if (!Directory.Exists(appPath))
                {
                    Logger.LogWarning($"Application folder does not exist: {appPath}");
                    return NotFound(new { message = "Application folder not found", path = appPath });
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = appPath,
                    UseShellExecute = true
                });

                BringWindowToFront("Application");

                return Ok(new { message = "Application folder opened", path = appPath });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error opening application folder");
                return StatusCode(500, new { message = "Error opening application folder", error = ex.Message });
            }
        }

        private void BringWindowToFront(string folderName)
        {
            try
            {
                System.Threading.Thread.Sleep(100);

                var explorerHandle = FindWindow("CabinetWClass", null);
                if (explorerHandle != IntPtr.Zero)
                {
                    SetForegroundWindow(explorerHandle);
                    ShowWindow(explorerHandle, SW_SHOW);
                    Logger.LogInfo("Brought explorer window to foreground");
                }
                else
                {
                    Logger.LogWarning("Could not find explorer window to bring to foreground");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to bring window to foreground");
            }
        }
    }
}
