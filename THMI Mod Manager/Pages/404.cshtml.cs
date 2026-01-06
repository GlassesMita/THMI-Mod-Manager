using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO;

namespace THMI_Mod_Manager.Pages
{
    public class _404Model : PageModel
    {
        public string FourZeroFourContent { get; private set; } = string.Empty;

        public void OnGet()
        {
            Response.StatusCode = 404;

            var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var filePath = Path.Combine(wwwrootPath, "staticpages", "404.html");

            if (System.IO.File.Exists(filePath))
            {
                FourZeroFourContent = System.IO.File.ReadAllText(filePath);
            }
            else
            {
                FourZeroFourContent = "<!DOCTYPE html><html><head><title>404 Not Found</title></head><body><h1>404 - Page Not Found</h1></body></html>";
            }
        }
    }
}
