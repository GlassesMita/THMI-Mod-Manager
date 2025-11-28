using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace THMI_Mod_Manager.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class SettingsModel : PageModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        private readonly ILogger<SettingsModel> _logger;
        private readonly THMI_Mod_Manager.Services.AppConfigManager _appConfig;

        public SettingsModel(ILogger<SettingsModel> logger, THMI_Mod_Manager.Services.AppConfigManager appConfig)
        {
            _logger = logger;
            _appConfig = appConfig;
        }

        public void OnGet()
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        }

        public IActionResult OnPostSaveLanguage([FromForm] string language, [FromForm] string status, [FromForm] bool useMystiaCursor)
        {
            if (string.IsNullOrEmpty(language))
            {
                return Page();
            }

            // Save into AppConfig.Schale under [Localization] Language=... (preserve naming like en_US)
            _appConfig.Set("[Localization]Language", language);
            
            // Save game status
            _appConfig.Set("[Game]Status", status);
            
            // Save cursor setting
            _appConfig.Set("[Cursor]UseMystiaCursor", useMystiaCursor.ToString());

            // Optionally reload configuration
            _appConfig.Reload();

            // Redirect to GET to show updated selection
            return RedirectToPage();
        }
    }
}