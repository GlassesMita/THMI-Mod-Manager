using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace THMI_Mod_Manager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FolderController : ControllerBase
    {
        private readonly ILogger<FolderController> _logger;
        private readonly IConfiguration _configuration;

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_SHOW = 5;

        public FolderController(ILogger<FolderController> logger, IConfiguration configuration)
        {
            _logger = logger;
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
                _logger.LogInformation("Save folder path: {SaveFolderPath}", saveFolderPath);

                if (!Directory.Exists(saveFolderPath))
                {
                    _logger.LogWarning("Save folder does not exist: {SaveFolderPath}", saveFolderPath);
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
                _logger.LogError(ex, "Error opening save folder");
                return StatusCode(500, new { message = "Error opening save folder", error = ex.Message });
            }
        }

        [HttpPost("open-bepinex-log-folder")]
        public IActionResult OpenBepInExLogFolder()
        {
            try
            {
                var gamePath = AppContext.BaseDirectory;
                _logger.LogInformation("BepInEx log folder using application running directory: {GamePath}", gamePath);

                var logFolderPath = Path.Combine(gamePath, "BepInEx");
                _logger.LogInformation("BepInEx log folder path: {LogFolderPath}", logFolderPath);

                if (!Directory.Exists(logFolderPath))
                {
                    _logger.LogWarning("BepInEx log folder does not exist: {LogFolderPath}", logFolderPath);
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
                _logger.LogError(ex, "Error opening BepInEx log folder");
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
                    _logger.LogWarning($"Application folder does not exist: {appPath}");
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
                _logger.LogError(ex, "Error opening application folder");
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
                    _logger.LogInformation("Brought explorer window to foreground");
                }
                else
                {
                    _logger.LogWarning("Could not find explorer window to bring to foreground");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to bring window to foreground");
            }
        }
    }
}
