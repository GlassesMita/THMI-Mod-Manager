using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace THMI_Mod_Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AboutController : ControllerBase
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int ShellAbout(
            IntPtr hWnd,
            string szApp,
            string szOtherStuff,
            IntPtr hIcon);

        [DllImport("shell32.dll")]
        private static extern IntPtr ExtractIcon(
            IntPtr hInst,
            string lpszExeFileName,
            int nIconIndex);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [HttpPost]
        public IActionResult ShowAbout([FromBody] AboutRequest? request = null)
        {
            var appName = request?.AppName ?? "THMI Mod Manager";
            var version = request?.Version ?? "0.10.0";

            IntPtr hIcon = IntPtr.Zero;
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
                }

                ShellAbout(
                    IntPtr.Zero,
                    appName,
                    $"{appName} {version}",
                    hIcon);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
            finally
            {
                if (hIcon != IntPtr.Zero)
                {
                    DestroyIcon(hIcon);
                }
            }
        }
    }

    public class AboutRequest
    {
        public string? AppName { get; set; }
        public string? Version { get; set; }
    }
}
