using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager.Pages
{
    public class SetupWizardModel : PageModel
    {
        private readonly ILogger<SetupWizardModel> _logger;
        private readonly AppConfigManager _appConfig;
        private readonly string _localizationPath;
        private readonly Dictionary<string, Dictionary<string, string>> _localizationCache = new();

        [BindProperty]
        public string SelectedLanguage { get; set; } = "en_US";

        [BindProperty]
        public string ThemeColor { get; set; } = "#c670ff";

        public string Message { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;

        public SetupWizardModel(ILogger<SetupWizardModel> logger, AppConfigManager appConfig, IWebHostEnvironment env)
        {
            _logger = logger;
            _appConfig = appConfig;
            _localizationPath = Path.Combine(env.ContentRootPath, "Localization");
            LoadLocalizationCache();
        }

        private void LoadLocalizationCache()
        {
            if (!System.IO.Directory.Exists(_localizationPath)) return;

            foreach (var file in System.IO.Directory.GetFiles(_localizationPath, "*.ini"))
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string? currentSection = null;

                foreach (var rawLine in System.IO.File.ReadAllLines(file, Encoding.UTF8))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
                        continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;

                    var k = line.Substring(0, idx).Trim();
                    var v = line.Substring(idx + 1).Trim();

                    var storeKey = string.IsNullOrEmpty(currentSection) ? k : $"{currentSection}:{k}";
                    dict[storeKey] = v;
                }

                _localizationCache[fileName] = dict;
            }
        }

        public string GetLocalizedForLanguage(string key, string language, string? defaultValue = null)
        {
            if (string.IsNullOrEmpty(key)) return defaultValue ?? key;

            var normalizedLanguage = language?.Replace('_', '-') ?? string.Empty;

            if (!string.IsNullOrEmpty(language) && _localizationCache.TryGetValue(language, out var dict1) && dict1.TryGetValue(key, out var value1))
                return value1;

            if (!string.IsNullOrEmpty(normalizedLanguage) && _localizationCache.TryGetValue(normalizedLanguage, out var dict2) && dict2.TryGetValue(key, out var value2))
                return value2;

            if (!string.IsNullOrEmpty(normalizedLanguage))
            {
                var parts = normalizedLanguage.Split('-');
                var neutral = parts.Length > 0 ? parts[0] : normalizedLanguage;
                if (!string.IsNullOrEmpty(neutral) && _localizationCache.TryGetValue(neutral, out var dict3) && dict3.TryGetValue(key, out var value3))
                    return value3;
            }

            return defaultValue ?? key;
        }

        public void OnGet(string? language = null)
        {
            if (!string.IsNullOrEmpty(language))
            {
                SelectedLanguage = language;
            }

            var isDevBuild = _appConfig.Get("[Dev]IsDevBuild", "False");
            if (!string.IsNullOrEmpty(isDevBuild) && bool.Parse(isDevBuild))
            {
                Message = _appConfig.GetLocalized("SetupWizard:DevModeWarning", "当前处于开发模式，跳过设置向导。");
                MessageType = "warning";
            }
        }

        public IActionResult OnPost()
        {
            try
            {
                _appConfig.Set("[Localization]Language", SelectedLanguage);
                _appConfig.Set("[App]ThemeColor", ThemeColor);
                _appConfig.Set("[App]IsFirstRun", "False");

                var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var semanticVersion = $"{assemblyVersion?.Major ?? 0}.{assemblyVersion?.Minor ?? 0}.{assemblyVersion?.Build ?? 0}";
                _appConfig.Set("[App]Version", semanticVersion);

                _logger.LogInformation("Setup wizard completed. Language: {Language}, ThemeColor: {ThemeColor}, Version: {Version}", SelectedLanguage, ThemeColor, semanticVersion);

                return RedirectToPage("/Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during setup wizard completion");
                Message = _appConfig.GetLocalized("SetupWizard:SaveError", "保存配置时出错，请重试。");
                MessageType = "danger";
                return Page();
            }
        }
    }
}
