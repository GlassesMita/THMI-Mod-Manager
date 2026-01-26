using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
using THMI_Mod_Manager.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Reflection;
using System.Xml.Linq;

namespace THMI_Mod_Manager.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class SettingsModel : PageModel
    {
        private static string? _cachedCsprojVersion;

        private static string ReadCsprojVersion()
        {
            if (_cachedCsprojVersion != null)
                return _cachedCsprojVersion;

            try
            {
                var csprojPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "THMI Mod Manager.csproj");
                if (!System.IO.File.Exists(csprojPath))
                {
                    csprojPath = Path.Combine(AppContext.BaseDirectory, "THMI Mod Manager.csproj");
                }
                if (!System.IO.File.Exists(csprojPath))
                {
                    csprojPath = "c:\\Users\\Mila\\source\\repos\\THMI Mod Manager\\THMI Mod Manager\\THMI Mod Manager.csproj";
                }

                if (System.IO.File.Exists(csprojPath))
                {
                    var doc = XDocument.Load(csprojPath);
                    var versionElement = doc.Root?.Element("PropertyGroup")?.Element("Version");
                    if (versionElement != null && !string.IsNullOrEmpty(versionElement.Value))
                    {
                        _cachedCsprojVersion = versionElement.Value;
                        return _cachedCsprojVersion;
                    }
                }
            }
            catch
            {
            }

            _cachedCsprojVersion = "0.0.0";
            return _cachedCsprojVersion;
        }
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

        // 光标设置属性
        public bool UseOsuCursor { get; set; }
        
        // 新的光标类型属性
        public string CursorType { get; set; } = "default";
        
        // 主题色属性
        public string ThemeColor { get; set; } = "#c670ff";
        
        // 游戏启动模式设置属性
        public string LaunchMode { get; set; } = "steam_launch"; // steam_launch 或 external_program
        public string LauncherPath { get; set; } = ""; // 用户指定外部程序路径
        
        // 修改应用标题设置属性
        public bool ModifyTitle { get; set; } = true; // 是否修改应用标题
        
        // 更新检查设置属性
        public bool AutoCheckUpdates { get; set; } = true; // 是否自动检查更新
        public string UpdateFrequency { get; set; } = "startup"; // 更新频率：startup, weekly, monthly
        
        // 通知设置属性
        public bool EnableNotifications { get; set; } = false; // 是否启用浏览器通知
        
        // 日期时间显示设置属性
        public bool ShowSeconds { get; set; } = false; // 是否显示秒
        public bool Use12Hour { get; set; } = false; // 是否使用12小时制
        public string DateFormat { get; set; } = "yyyy-mm-dd"; // 日期格式
        
        // Mod信息属性
        public string ModName { get; set; } = "THMI Mod Manager";
        public string ModVersion { get; set; } = ReadCsprojVersion();

        public SettingsModel(ILogger<SettingsModel> logger, THMI_Mod_Manager.Services.AppConfigManager appConfig)
        {
            _logger = logger;
            _appConfig = appConfig;
        }

        public void OnGet()
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

            try
            {
                var languageValue = _appConfig.Get("[Localization]Language", "en_US");
                CurrentLanguage = languageValue ?? "en_US";
                SelectedLanguage = CurrentLanguage ?? "en_US";

                var devBuildValue = _appConfig.Get("[Dev]IsDevBuild", "false");
                IsDevMode = devBuildValue?.ToLower() == "true";
                
                var cveWarningValue = _appConfig.Get("[Dev]ShowCVEWarning", "true");
                ShowCVEWarning = cveWarningValue?.ToLower() != "false";

                var cursorValue = _appConfig.Get("[Cursor]UseMystiaCursor", "false");
                UseOsuCursor = cursorValue?.ToLower() == "true";
                
                var cursorTypeValue = _appConfig.Get("[Cursor]CursorType", "default");
                CursorType = cursorTypeValue ?? "default";
                
                if (UseOsuCursor && CursorType == "default")
                {
                    CursorType = "osu";
                    _appConfig.Set("[Cursor]CursorType", "osu");
                }
                
                var themeColorValue = _appConfig.Get("[App]ThemeColor", "#c670ff");
                ThemeColor = themeColorValue ?? "#c670ff";
                
                var launchModeValue = _appConfig.Get("[Game]LaunchMode", "steam_launch");
                LaunchMode = launchModeValue ?? "steam_launch";
                
                var launcherPathValue = _appConfig.Get("[Game]LauncherPath", "");
                LauncherPath = launcherPathValue ?? "";
                
                var modifyTitleValue = _appConfig.Get("[Game]ModifyTitle", "true");
                ModifyTitle = modifyTitleValue?.ToLower() != "false";
                
                var autoCheckUpdatesValue = _appConfig.Get("[Updates]CheckForUpdates", "true");
                AutoCheckUpdates = autoCheckUpdatesValue?.ToLower() != "false";
                
                var updateFrequencyValue = _appConfig.Get("[Updates]UpdateFrequency", "startup");
                UpdateFrequency = updateFrequencyValue ?? "startup";
                
                var enableNotificationsValue = _appConfig.Get("[Notifications]Enable", "false");
                EnableNotifications = enableNotificationsValue?.ToLower() == "true";
                
                var showSecondsValue = _appConfig.Get("[DateTime]ShowSeconds", "false");
                ShowSeconds = showSecondsValue?.ToLower() == "true";
                
                var use12HourValue = _appConfig.Get("[DateTime]Use12Hour", "false");
                Use12Hour = use12HourValue?.ToLower() == "true";
                
                var dateFormatValue = _appConfig.Get("[DateTime]DateFormat", "yyyy-mm-dd");
                DateFormat = dateFormatValue ?? "yyyy-mm-dd";
                
                // Load program version information from AppConfig
                try
                {
                    ModName = _appConfig.Get("[App]Name", "THMI Mod Manager") ?? "THMI Mod Manager";
                }
                catch
                {
                    // Keep default values
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public IActionResult OnPostSaveLanguage(string language, string status, bool useOsuCursor, bool useCustomCursor, string cursorType, string themeColor, string launchMode, string launcherPath, string modsPath, string gamePath, bool modifyTitle, bool autoCheckUpdates, string updateFrequency, bool enableNotifications, bool showSeconds, bool use12Hour, string dateFormat)
        {
            if (string.IsNullOrEmpty(language))
            {
                return Page();
            }

            try
            {
                // Save into AppConfig.Schale under [Localization] Language=... (preserve naming like en_US)
                _appConfig.Set("[Localization]Language", language);
                
                // Save game status
                _appConfig.Set("[Game]Status", status);
                
                // Save cursor setting (向后兼容)
                _appConfig.Set("[Cursor]UseMystiaCursor", useOsuCursor.ToString());
                
                // Save new cursor type setting
                if (!string.IsNullOrEmpty(cursorType))
                {
                    _appConfig.Set("[Cursor]CursorType", cursorType);
                }
                
                // Save theme color setting
                if (!string.IsNullOrEmpty(themeColor))
                {
                    _appConfig.Set("[App]ThemeColor", themeColor);
                }
                
                // Save game launch mode settings
                if (!string.IsNullOrEmpty(launchMode))
                {
                    _appConfig.Set("[Game]LaunchMode", launchMode);
                }
                
                // Save user-specified external program path
                _appConfig.Set("[Game]LauncherPath", launcherPath);
                
                // Save modify title setting
                _appConfig.Set("[Game]ModifyTitle", modifyTitle.ToString());

                // Save auto check updates setting
                _appConfig.Set("[Updates]CheckForUpdates", autoCheckUpdates.ToString());

                // Save update frequency setting
                if (!string.IsNullOrEmpty(updateFrequency))
                {
                    _appConfig.Set("[Updates]UpdateFrequency", updateFrequency);
                }

                // Save enable notifications setting
                _appConfig.Set("[Notifications]Enable", enableNotifications.ToString());

                // Save date/time display settings
                _appConfig.Set("[DateTime]ShowSeconds", showSeconds.ToString());
                _appConfig.Set("[DateTime]Use12Hour", use12Hour.ToString());
                if (!string.IsNullOrEmpty(dateFormat))
                {
                    _appConfig.Set("[DateTime]DateFormat", dateFormat);
                }

                // Save custom cursor setting
                _appConfig.Set("[Cursor]UseCustomCursor", useCustomCursor.ToString());

                // Save mods path setting - automatically set to current directory + Mods folder
                string autoModsPath = Path.Combine(AppContext.BaseDirectory, "Mods");
                if (!Directory.Exists(autoModsPath))
                {
                    Directory.CreateDirectory(autoModsPath);
                }
                _appConfig.Set("[App]ModsPath", autoModsPath);

                // Save game path setting - automatically set to current directory
                string autoGamePath = AppContext.BaseDirectory;
                _appConfig.Set("[App]GamePath", autoGamePath);

                // Optionally reload configuration
                _appConfig.Reload();
                Logger.LogInfo("Configuration reloaded successfully");
                
                // Return success response for AJAX request
                return new JsonResult(new { success = true, message = "Settings saved successfully!" });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving settings: {ex.Message}");
                return new JsonResult(new { success = false, message = $"Error saving settings: {ex.Message}" });
            }
        }

        public IActionResult OnPostSaveCursorSettings(string cursorType)
        {
            Logger.LogInfo($"Saving cursor settings - CursorType: {cursorType}");
            
            if (string.IsNullOrEmpty(cursorType))
            {
                Logger.LogWarning("CursorType parameter is empty, returning to page");
                return new JsonResult(new { success = false, message = "Cursor type cannot be empty" });
            }

            try
            {
                // Save cursor type setting
                _appConfig.Set("[Cursor]CursorType", cursorType);
                Logger.LogInfo($"Cursor type saved: {cursorType}");

                // Optionally reload configuration
                _appConfig.Reload();
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Error saving cursor settings: {ex.Message}" });
            }

            // Return success
            return new JsonResult(new { success = true, message = "Cursor settings saved successfully!" });
        }

        public IActionResult OnPostSaveDeveloperSettings(bool devMode, bool showCVEWarning)
        {
            try
            {
                // Save developer settings to AppConfig.Schale
                _appConfig.Set("[Dev]IsDevBuild", devMode.ToString());
                _appConfig.Set("[Dev]ShowCVEWarning", showCVEWarning.ToString());

                // Reload configuration
                _appConfig.Reload();
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Error saving settings: {ex.Message}" });
            }

            // Return success
            return new JsonResult(new { success = true, message = "Developer settings saved successfully!" });
        }
    }
}