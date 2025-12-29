using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
using THMI_Mod_Manager.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
        
        // Mod信息属性
        public string ModName { get; set; } = "MetaIzakaya";
        public string ModVersion { get; set; } = "0.7.0";

        public SettingsModel(ILogger<SettingsModel> logger, THMI_Mod_Manager.Services.AppConfigManager appConfig)
        {
            _logger = logger;
            _appConfig = appConfig;
        }

        public void OnGet()
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            Logger.LogInfo($"Settings page accessed - RequestId: {RequestId}");

            try
            {
                var languageValue = _appConfig.Get("[Localization]Language", "en_US");
                CurrentLanguage = languageValue ?? "en_US";
                SelectedLanguage = CurrentLanguage ?? "en_US";
                Logger.LogInfo($"Loaded language settings: {CurrentLanguage}");

                var devBuildValue = _appConfig.Get("[Dev]IsDevBuild", "false");
                IsDevMode = devBuildValue?.ToLower() == "true";
                
                var cveWarningValue = _appConfig.Get("[Dev]ShowCVEWarning", "true");
                ShowCVEWarning = cveWarningValue?.ToLower() != "false";
                Logger.LogInfo($"Loaded developer settings - DevMode: {IsDevMode}, ShowCVEWarning: {ShowCVEWarning}");

                var cursorValue = _appConfig.Get("[Cursor]UseMystiaCursor", "false");
                UseOsuCursor = cursorValue?.ToLower() == "true";
                
                var cursorTypeValue = _appConfig.Get("[Cursor]CursorType", "default");
                CursorType = cursorTypeValue ?? "default";
                
                if (UseOsuCursor && CursorType == "default")
                {
                    CursorType = "osu";
                    _appConfig.Set("[Cursor]CursorType", "osu");
                    Logger.LogInfo("Converted legacy UseOsuCursor setting to new CursorType: osu");
                }
                
                Logger.LogInfo($"Loaded cursor settings: CursorType: {CursorType}");
                
                var themeColorValue = _appConfig.Get("[App]ThemeColor", "#c670ff");
                ThemeColor = themeColorValue ?? "#c670ff";
                Logger.LogInfo($"Loaded theme color: {ThemeColor}");
                
                var launchModeValue = _appConfig.Get("[Game]LaunchMode", "steam_launch");
                LaunchMode = launchModeValue ?? "steam_launch";
                
                var launcherPathValue = _appConfig.Get("[Game]LauncherPath", "");
                LauncherPath = launcherPathValue ?? "";
                
                var modifyTitleValue = _appConfig.Get("[Game]ModifyTitle", "true");
                ModifyTitle = modifyTitleValue?.ToLower() != "false";
                Logger.LogInfo($"Loaded game launch mode settings: LaunchMode: {LaunchMode}, LauncherPath: {LauncherPath}, ModifyTitle: {ModifyTitle}");
                
                var autoCheckUpdatesValue = _appConfig.Get("[Updates]CheckForUpdates", "true");
                AutoCheckUpdates = autoCheckUpdatesValue?.ToLower() != "false";
                Logger.LogInfo($"Loaded update settings: AutoCheckUpdates: {AutoCheckUpdates}");
                
                // Load mod information
                try
                {
                    ModName = "MetaIzakaya";
                    ModVersion = "0.7.0";
                    Logger.LogInfo($"Loaded mod information: {ModName} v{ModVersion}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error loading mod information: {ex.Message}");
                    // Keep default values
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading settings: {ex.Message}");
                throw;
            }
        }

        public IActionResult OnPostSaveLanguage(string language, string status, bool useOsuCursor, bool useCustomCursor, string cursorType, string themeColor, string launchMode, string launcherPath, string modsPath, string gamePath, bool modifyTitle, bool autoCheckUpdates)
        {
            Logger.LogInfo($"Saving settings - Language: {language}, Status: {status}, UseOsuCursor: {useOsuCursor}, UseCustomCursor: {useCustomCursor}, CursorType: {cursorType}, ThemeColor: {themeColor}, LaunchMode: {launchMode}, LauncherPath: {launcherPath}, ModsPath: {modsPath}, GamePath: {gamePath}, ModifyTitle: {modifyTitle}, AutoCheckUpdates: {autoCheckUpdates}");
            
            
            if (string.IsNullOrEmpty(language))
            {
                Logger.LogWarning("Language parameter is empty, returning to page");
                return Page();
            }

            try
            {
                // Save into AppConfig.Schale under [Localization] Language=... (preserve naming like en_US)
                _appConfig.Set("[Localization]Language", language);
                Logger.LogInfo($"Language setting saved: {language}");
                
                // Save game status
                _appConfig.Set("[Game]Status", status);
                Logger.LogInfo($"Game status saved: {status}");
                
                // Save cursor setting (向后兼容)
                _appConfig.Set("[Cursor]UseMystiaCursor", useOsuCursor.ToString());
                Logger.LogInfo($"Legacy cursor setting saved: {useOsuCursor}");
                
                // Save new cursor type setting
                if (!string.IsNullOrEmpty(cursorType))
                {
                    _appConfig.Set("[Cursor]CursorType", cursorType);
                    Logger.LogInfo($"Cursor type saved: {cursorType}");
                }
                
                // Save theme color setting
                if (!string.IsNullOrEmpty(themeColor))
                {
                    _appConfig.Set("[App]ThemeColor", themeColor);
                    Logger.LogInfo($"Theme color saved: {themeColor}");
                }
                
                // Save game launch mode settings
                if (!string.IsNullOrEmpty(launchMode))
                {
                    _appConfig.Set("[Game]LaunchMode", launchMode);
                    Logger.LogInfo($"Game launch mode saved: {launchMode}");
                }
                
                // Save user-specified external program path
                _appConfig.Set("[Game]LauncherPath", launcherPath);
                Logger.LogInfo($"User-specified external program path saved: {launcherPath}");
                
                // Save modify title setting
                _appConfig.Set("[Game]ModifyTitle", modifyTitle.ToString());
                Logger.LogInfo($"Modify title setting saved: {modifyTitle}");

                // Save auto check updates setting
                _appConfig.Set("[Updates]CheckForUpdates", autoCheckUpdates.ToString());
                Logger.LogInfo($"Auto check updates setting saved: {autoCheckUpdates}");

                // Save custom cursor setting
                _appConfig.Set("[Cursor]UseCustomCursor", useCustomCursor.ToString());
                Logger.LogInfo($"Custom cursor setting saved: {useCustomCursor}");

                // Save mods path setting - automatically set to current directory + Mods folder
                string autoModsPath = Path.Combine(AppContext.BaseDirectory, "Mods");
                // Ensure the Mods directory exists
                if (!Directory.Exists(autoModsPath))
                {
                    Directory.CreateDirectory(autoModsPath);
                }
                _appConfig.Set("[App]ModsPath", autoModsPath);
                Logger.LogInfo($"Mods path automatically set to: {autoModsPath}");

                // Save game path setting - automatically set to current directory
                string autoGamePath = AppContext.BaseDirectory;
                _appConfig.Set("[App]GamePath", autoGamePath);
                Logger.LogInfo($"Game path automatically set to: {autoGamePath}");

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
                Logger.LogInfo("Configuration reloaded successfully after cursor settings update");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving cursor settings: {ex.Message}");
                return new JsonResult(new { success = false, message = $"Error saving cursor settings: {ex.Message}" });
            }

            // Return success
            return new JsonResult(new { success = true, message = "Cursor settings saved successfully!" });
        }

        public IActionResult OnPostSaveDeveloperSettings(bool devMode, bool showCVEWarning)
        {
            Logger.LogInfo($"Saving developer settings - DevMode: {devMode}, ShowCVEWarning: {showCVEWarning}");
            
            try
            {
                // Save developer settings to AppConfig.Schale
                _appConfig.Set("[Dev]IsDevBuild", devMode.ToString());
                _appConfig.Set("[Dev]ShowCVEWarning", showCVEWarning.ToString());
                Logger.LogInfo("Developer settings saved successfully");

                // Reload configuration
                _appConfig.Reload();
                Logger.LogInfo("Configuration reloaded after developer settings update");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving developer settings: {ex.Message}");
                return new JsonResult(new { success = false, message = $"Error saving settings: {ex.Message}" });
            }

            // Return success
            return new JsonResult(new { success = true, message = "Developer settings saved successfully!" });
        }
    }
}