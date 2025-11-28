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

        [BindProperty]
        public string SelectedLanguage { get; set; } = "en_US";

        public string CurrentLanguage { get; set; } = "en_US";

        // 开发者设置属性
        public bool IsDevMode { get; set; }
        public bool ShowCVEWarning { get; set; }

        public SettingsModel(ILogger<SettingsModel> logger, THMI_Mod_Manager.Services.AppConfigManager appConfig)
        {
            _logger = logger;
            _appConfig = appConfig;
        }

        public void OnGet()
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

            // Load current language from config
            CurrentLanguage = _appConfig.Get("[Localization]Language", "en_US");
            SelectedLanguage = CurrentLanguage;

            // 加载开发者设置
            IsDevMode = _appConfig.Get("[Dev]IsDevBuild", "false").ToLower() == "true";
            ShowCVEWarning = _appConfig.Get("[Dev]ShowCVEWarning", "true").ToLower() != "false";
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

        public IActionResult OnPostSaveDeveloperSettings([FromForm] bool devMode, [FromForm] bool showCVEWarning)
        {
            // Save developer settings to AppConfig.Schale
            _appConfig.Set("[Dev]IsDevBuild", devMode.ToString());
            _appConfig.Set("[Dev]ShowCVEWarning", showCVEWarning.ToString());

            // Reload configuration
            _appConfig.Reload();

            // Return success
            return new JsonResult(new { success = true, message = "Developer settings saved successfully!" });
        }
    }
}