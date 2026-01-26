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
        public string ThemeColor { get; set; } = "#ACCEED";

        [BindProperty]
        public bool AutoCheckUpdates { get; set; } = true;

        [BindProperty]
        public string UpdateFrequency { get; set; } = "startup";

        [BindProperty]
        public bool EnableNotifications { get; set; } = false;

        [BindProperty]
        public bool ModifyTitle { get; set; } = true;

        [BindProperty]
        public bool SkipRemainingQuestions { get; set; } = false;

        public string Message { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public bool IsReConfiguration { get; set; } = false;
        public bool IsConfigured { get; set; } = false;
        public string? ExistingLanguage { get; set; }
        public string? ExistingThemeColor { get; set; }

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

        public void OnGet(string? language = null, bool? reconfigure = null)
        {
            var isDevBuild = _appConfig.Get("[Dev]IsDevBuild", "False");

            if (!string.IsNullOrEmpty(isDevBuild) && bool.Parse(isDevBuild))
            {
                Message = _appConfig.GetLocalized("SetupWizard:DevModeWarning", "当前处于开发模式，跳过设置向导。");
                MessageType = "warning";
                return;
            }

            var existingLanguage = _appConfig.Get("Localization", "Language");
            var isFirstRun = _appConfig.Get("App", "IsFirstRun");
            var allSections = _appConfig.GetAllSections();
            var hasValidConfig = !string.IsNullOrEmpty(existingLanguage) && 
                                 isFirstRun?.ToLower() != "true" && 
                                 allSections.Count > 0;

            if (hasValidConfig)
            {
                ExistingLanguage = existingLanguage;
                ExistingThemeColor = _appConfig.Get("App", "ThemeColor");
                SelectedLanguage = existingLanguage;
                ThemeColor = ExistingThemeColor ?? "#ACCEED";

                AutoCheckUpdates = bool.TryParse(_appConfig.Get("Updates", "CheckForUpdates"), out var autoCheck) && autoCheck;
                UpdateFrequency = _appConfig.Get("Updates", "UpdateFrequency") ?? "startup";
                EnableNotifications = bool.TryParse(_appConfig.Get("Notifications", "Enable"), out var enableNotif) && enableNotif;
                ModifyTitle = bool.TryParse(_appConfig.Get("Game", "ModifyTitle"), out var modify) && modify;

                if (reconfigure == true)
                {
                    IsReConfiguration = true;
                    Message = _appConfig.GetLocalized("SetupWizard:AlreadyConfigured", "系统已配置完成，您可以重新调整设置。");
                    MessageType = "info";
                }
                else
                {
                    IsConfigured = true;
                }
            }

            if (!string.IsNullOrEmpty(language))
            {
                SelectedLanguage = language;
            }
        }

        public IActionResult OnPost()
        {
            try
            {
                _appConfig.Set("[Localization]Language", SelectedLanguage);
                _appConfig.Set("[App]ThemeColor", ThemeColor);

                if (SkipRemainingQuestions)
                {
                    _appConfig.Set("[App]IsFirstRun", "False");

                    var semanticVersion = Assembly.GetExecutingAssembly()
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
                        ?? "0.0.0";
                    _appConfig.Set("[App]Version", semanticVersion);

                    return RedirectToPage("/Index");
                }

                _appConfig.Set("[Updates]CheckForUpdates", AutoCheckUpdates.ToString());
                _appConfig.Set("[Updates]UpdateFrequency", UpdateFrequency);
                _appConfig.Set("[Notifications]Enable", EnableNotifications.ToString());
                _appConfig.Set("[Game]ModifyTitle", ModifyTitle.ToString());

                _appConfig.Set("[App]IsFirstRun", "False");

                var semanticVersion2 = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
                    ?? "0.0.0";
                _appConfig.Set("[App]Version", semanticVersion2);

                return RedirectToPage("/Index");
            }
            catch (Exception ex)
            {
                Message = _appConfig.GetLocalized("SetupWizard:SaveError", "保存配置时出错，请重试。");
                MessageType = "danger";
                return Page();
            }
        }
    }
}
