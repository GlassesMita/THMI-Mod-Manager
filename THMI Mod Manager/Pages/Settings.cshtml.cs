using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
using THMI_Mod_Manager.Services;

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
                // Load current language from config
                CurrentLanguage = _appConfig.Get("[Localization]Language", "en_US");
                SelectedLanguage = CurrentLanguage;
                Logger.LogInfo($"Loaded language settings: {CurrentLanguage}");

                // 加载开发者设置
                IsDevMode = _appConfig.Get("[Dev]IsDevBuild", "false").ToLower() == "true";
                ShowCVEWarning = _appConfig.Get("[Dev]ShowCVEWarning", "true").ToLower() != "false";
                Logger.LogInfo($"Loaded developer settings - DevMode: {IsDevMode}, ShowCVEWarning: {ShowCVEWarning}");

                // 加载光标设置（向后兼容）
                UseOsuCursor = _appConfig.Get("[Cursor]UseMystiaCursor", "false").ToLower() == "true";
                
                // 加载新的光标类型设置
                CursorType = _appConfig.Get("[Cursor]CursorType", "default");
                
                // 向后兼容：如果旧的UseOsuCursor为true，则转换为新的osu类型
                if (UseOsuCursor && CursorType == "default")
                {
                    CursorType = "osu";
                    _appConfig.Set("[Cursor]CursorType", "osu");
                    Logger.LogInfo("Converted legacy UseOsuCursor setting to new CursorType: osu");
                }
                
                Logger.LogInfo($"Loaded cursor settings: CursorType: {CursorType}");
                
                // 加载主题色设置
                ThemeColor = _appConfig.Get("[App]ThemeColor", "#c670ff");
                Logger.LogInfo($"Loaded theme color: {ThemeColor}");
                
                // 加载游戏启动模式设置
                LaunchMode = _appConfig.Get("[Game]LaunchMode", "steam_launch");
                LauncherPath = _appConfig.Get("[Game]LauncherPath", "");
                Logger.LogInfo($"Loaded game launch mode settings: LaunchMode: {LaunchMode}, LauncherPath: {LauncherPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading settings: {ex.Message}");
                throw;
            }
        }

        public IActionResult OnPostSaveLanguage([FromForm] string language, [FromForm] string status, [FromForm] bool useOsuCursor, [FromForm] bool useCustomCursor, [FromForm] string cursorType, [FromForm] string themeColor, [FromForm] string launchMode, [FromForm] string launcherPath, [FromForm] string modsPath, [FromForm] string gamePath)
        {
            Logger.LogInfo($"Saving settings - Language: {language}, Status: {status}, UseOsuCursor: {useOsuCursor}, UseCustomCursor: {useCustomCursor}, CursorType: {cursorType}, ThemeColor: {themeColor}, LaunchMode: {launchMode}, LauncherPath: {launcherPath}, ModsPath: {modsPath}, GamePath: {gamePath}");
            
            
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
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving settings: {ex.Message}");
                throw;
            }

            // Redirect to GET to show updated selection
            return RedirectToPage();
        }

        public IActionResult OnPostSaveCursorSettings([FromForm] string cursorType)
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

        public IActionResult OnPostSaveDeveloperSettings([FromForm] bool devMode, [FromForm] bool showCVEWarning)
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