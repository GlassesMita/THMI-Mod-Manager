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
        
        // 图标字体设置属性
        public string IconFont { get; set; } = "mdl2"; // mdl2 或 fluent
        
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
        public string DateSeparator { get; set; } = "-"; // 日期分隔符
        
        // Mod信息属性
        public string ModName { get; set; } = "THMI Mod Manager";
        public string ModVersion { get; set; } = ReadCsprojVersion();

        // 游戏路径属性
        public string GamePath { get; set; } = "";

        // BepInEx配置属性
        public string BepInExConfigPath { get; set; } = "";
        public bool EnableAssemblyCache { get; set; } = true;
        public string DetourProviderType { get; set; } = "Default";
        public string HarmonyLogChannels { get; set; } = "Warn, Error";
        public bool UpdateInteropAssemblies { get; set; } = true;
        public string UnityBaseLibrariesSource { get; set; } = "https://unity.bepinex.dev/libraries/{VERSION}.zip";
        public string IL2CPPInteropAssembliesPath { get; set; } = "{BepInEx}";
        public bool PreloadIL2CPPInteropAssemblies { get; set; } = true;
        public bool UnityLogListening { get; set; } = true;
        public bool ConsoleEnabled { get; set; } = true;
        public bool ConsolePreventClose { get; set; } = false;
        public bool ConsoleShiftJisEncoding { get; set; } = false;
        public string ConsoleStandardOutType { get; set; } = "Auto";
        public string ConsoleLogLevels { get; set; } = "Fatal, Error, Warning, Message, Info";
        public bool DiskLogEnabled { get; set; } = true;
        public bool DiskLogAppend { get; set; } = false;
        public string DiskLogLevels { get; set; } = "Fatal, Error, Warning, Message, Info";
        public bool DiskLogInstantFlushing { get; set; } = false;
        public int DiskLogConcurrentFileLimit { get; set; } = 5;
        public bool WriteUnityLog { get; set; } = false;
        public string HarmonyBackend { get; set; } = "auto";
        public bool DumpAssemblies { get; set; } = false;
        public bool LoadDumpedAssemblies { get; set; } = false;
        public bool BreakBeforeLoadAssemblies { get; set; } = false;

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
                
                var iconFontValue = _appConfig.Get("[Icon]Font", "mdl2");
                IconFont = iconFontValue ?? "mdl2";
                
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
                
                var dateSeparatorValue = _appConfig.Get("[DateTime]DateSeparator", "-");
                DateSeparator = dateSeparatorValue ?? "-";
                
                // Load program version information from AppConfig
                try
                {
                    ModName = _appConfig.Get("[App]Name", "THMI Mod Manager") ?? "THMI Mod Manager";
                }
                catch
                {
                    // Keep default values
                }
                
                // Load game path - auto-detect if not set
                var gamePathValue = _appConfig.Get("[Game]GamePath", "");
                GamePath = gamePathValue ?? "";
                
                // If game path not found, try to detect from AppContext.BaseDirectory (executable directory)
                if (string.IsNullOrEmpty(GamePath))
                {
                    // BepInEx is typically at {GamePath}/BepInEx
                    // Try executable directory + BepInEx/config first
                    var executableDir = AppContext.BaseDirectory;
                    var bepInExConfigFromExecutable = Path.Combine(executableDir, "BepInEx", "config", "BepInEx.cfg");
                    
                    if (System.IO.File.Exists(bepInExConfigFromExecutable))
                    {
                        GamePath = executableDir;
                    }
                    else
                    {
                        // Try parent directories
                        var currentDir = executableDir;
                        for (int i = 0; i < 5; i++)
                        {
                            var parentDir = Directory.GetParent(currentDir)?.FullName;
                            if (string.IsNullOrEmpty(parentDir)) break;
                            
                            var testPath = Path.Combine(parentDir, "BepInEx", "config", "BepInEx.cfg");
                            if (System.IO.File.Exists(testPath))
                            {
                                GamePath = parentDir;
                                break;
                            }
                            currentDir = parentDir;
                        }
                    }
                }
                
                // If still empty, try to detect from ModsPath
                if (string.IsNullOrEmpty(GamePath))
                {
                    var modsPathValue = _appConfig.Get("[App]ModsPath", "");
                    if (!string.IsNullOrEmpty(modsPathValue))
                    {
                        var modsPathDir = Directory.GetParent(modsPathValue)?.FullName;
                        if (!string.IsNullOrEmpty(modsPathDir))
                        {
                            if (modsPathDir.EndsWith("Mods"))
                            {
                                GamePath = Directory.GetParent(modsPathDir)?.FullName ?? "";
                            }
                            else if (modsPathDir.EndsWith("BepInEx") || modsPathDir.Contains("BepInEx"))
                            {
                                var bepInExParent = Directory.GetParent(modsPathDir);
                                if (bepInExParent != null && bepInExParent.Name == "config")
                                {
                                    GamePath = Directory.GetParent(bepInExParent.FullName)?.FullName ?? "";
                                }
                                else
                                {
                                    GamePath = Directory.GetParent(modsPathDir)?.FullName ?? "";
                                }
                            }
                            else
                            {
                                GamePath = modsPathDir;
                            }
                        }
                    }
                }
                
                // If still empty, try common Steam paths
                if (string.IsNullOrEmpty(GamePath))
                {
                    var steamPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    var commonSteamPaths = new[]
                    {
                        Path.Combine(steamPath, "Steam", "steamapps", "common", "Touhou Mystia Izakaya"),
                        @"f:\SteamLibrary\steamapps\common\Touhou Mystia Izakaya",
                        @"d:\SteamLibrary\steamapps\common\Touhou Mystia Izakaya",
                    };
                    
                    foreach (var path in commonSteamPaths)
                    {
                        if (Directory.Exists(path))
                        {
                            GamePath = path;
                            break;
                        }
                    }
                }
                
                // Auto-detect BepInEx config path
                string? autoDetectedBepInExPath = null;
                if (!string.IsNullOrEmpty(GamePath))
                {
                    var bepInExConfigDir = Path.Combine(GamePath, "BepInEx", "config");
                    var bepInExConfigFile = Path.Combine(bepInExConfigDir, "BepInEx.cfg");
                    if (System.IO.File.Exists(bepInExConfigFile))
                    {
                        autoDetectedBepInExPath = bepInExConfigFile;
                    }
                }
                
                // Use auto-detected path or fallback to saved path
                if (!string.IsNullOrEmpty(autoDetectedBepInExPath))
                {
                    BepInExConfigPath = autoDetectedBepInExPath;
                }
                else
                {
                    var bepInExConfigPathValue = _appConfig.Get("[BepInEx]ConfigPath", "");
                    BepInExConfigPath = bepInExConfigPathValue ?? "";
                }
                
                // Load BepInEx settings from config file if path is configured
                if (!string.IsNullOrEmpty(BepInExConfigPath) && System.IO.File.Exists(BepInExConfigPath))
                {
                    LoadBepInExSettings(BepInExConfigPath);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private void LoadBepInExSettings(string configPath)
        {
            try
            {
                _logger.LogInformation($"Loading BepInEx settings from: {configPath}");
                _logger.LogInformation($"File exists: {System.IO.File.Exists(configPath)}");
                
                var ini = IniFileHelper.LoadOrCreate(configPath);
                
                _logger.LogInformation($"INI data loaded, sections: {string.Join(", ", ini.GetModifiedKeys())}");
                
                // [Caching]
                var enableAssemblyCache = ini.GetBool("Caching", "EnableAssemblyCache", true);
                EnableAssemblyCache = enableAssemblyCache;
                _logger.LogInformation($"EnableAssemblyCache: {enableAssemblyCache}");
                
                // [Detours]
                DetourProviderType = ini.GetValue("Detours", "DetourProviderType", "Default");
                
                // [Harmony.Logger]
                HarmonyLogChannels = ini.GetValue("Harmony.Logger", "LogChannels", "Warn, Error");
                _logger.LogInformation("HarmonyLogChannels: {Value}", HarmonyLogChannels);
                
                // [IL2CPP]
                UpdateInteropAssemblies = ini.GetBool("IL2CPP", "UpdateInteropAssemblies", true);
                UnityBaseLibrariesSource = ini.GetValue("IL2CPP", "UnityBaseLibrariesSource", "https://unity.bepinex.dev/libraries/{VERSION}.zip");
                IL2CPPInteropAssembliesPath = ini.GetValue("IL2CPP", "IL2CPPInteropAssembliesPath", "{BepInEx}");
                PreloadIL2CPPInteropAssemblies = ini.GetBool("IL2CPP", "PreloadIL2CPPInteropAssemblies", true);
                
                // [Logging]
                UnityLogListening = ini.GetBool("Logging", "UnityLogListening", true);
                
                // [Logging.Console]
                ConsoleEnabled = ini.GetBool("Logging.Console", "Enabled", true);
                ConsolePreventClose = ini.GetBool("Logging.Console", "PreventClose", false);
                ConsoleShiftJisEncoding = ini.GetBool("Logging.Console", "ShiftJisEncoding", false);
                ConsoleStandardOutType = ini.GetValue("Logging.Console", "StandardOutType", "Auto");
                ConsoleLogLevels = ini.GetValue("Logging.Console", "LogLevels", "Fatal, Error, Warning, Message, Info");
                
                // [Logging.Disk]
                DiskLogEnabled = ini.GetBool("Logging.Disk", "Enabled", true);
                DiskLogAppend = ini.GetBool("Logging.Disk", "AppendLog", false);
                DiskLogLevels = ini.GetValue("Logging.Disk", "LogLevels", "Fatal, Error, Warning, Message, Info");
                DiskLogInstantFlushing = ini.GetBool("Logging.Disk", "InstantFlushing", false);
                DiskLogConcurrentFileLimit = ini.GetInt("Logging.Disk", "ConcurrentFileLimit", 5);
                WriteUnityLog = ini.GetBool("Logging.Disk", "WriteUnityLog", false);
                
                // [Preloader]
                HarmonyBackend = ini.GetValue("Preloader", "HarmonyBackend", "auto");
                DumpAssemblies = ini.GetBool("Preloader", "DumpAssemblies", false);
                LoadDumpedAssemblies = ini.GetBool("Preloader", "LoadDumpedAssemblies", false);
                BreakBeforeLoadAssemblies = ini.GetBool("Preloader", "BreakBeforeLoadAssemblies", false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading BepInEx settings: {ex.Message}");
            }
        }

        public IActionResult OnPostSaveLanguage(string language, string status, bool useOsuCursor, bool useCustomCursor, string cursorType, string iconFont, string themeColor, string launchMode, string launcherPath, string modsPath, string gamePath, bool modifyTitle, bool autoCheckUpdates, string updateFrequency, bool enableNotifications, bool showSeconds, bool use12Hour, string dateFormat, string dateSeparator)
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
                
                // Save icon font setting
                if (!string.IsNullOrEmpty(iconFont))
                {
                    _appConfig.Set("[Icon]Font", iconFont);
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
                if (!string.IsNullOrEmpty(dateSeparator))
                {
                    _appConfig.Set("[DateTime]DateSeparator", dateSeparator);
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

        public IActionResult OnPostSaveBepInExSettings(
            string bepInExConfigPath,
            bool enableAssemblyCache,
            string detourProviderType,
            string harmonyLogChannels,
            bool updateInteropAssemblies,
            string unityBaseLibrariesSource,
            string il2cppInteropAssembliesPath,
            bool preloadIL2CPPInteropAssemblies,
            bool unityLogListening,
            bool consoleEnabled,
            bool consolePreventClose,
            bool consoleShiftJisEncoding,
            string consoleStandardOutType,
            string consoleLogLevels,
            bool diskLogEnabled,
            bool diskLogAppend,
            string diskLogLevels,
            bool diskLogInstantFlushing,
            int diskLogConcurrentFileLimit,
            bool writeUnityLog,
            string harmonyBackend,
            bool dumpAssemblies,
            bool loadDumpedAssemblies,
            bool breakBeforeLoadAssemblies)
        {
            try
            {
                // Save BepInEx config path
                _appConfig.Set("[BepInEx]ConfigPath", bepInExConfigPath ?? "");
                
                // Save to BepInEx.cfg if path is valid
                if (!string.IsNullOrEmpty(bepInExConfigPath) && System.IO.File.Exists(bepInExConfigPath))
                {
                    var ini = IniFileHelper.LoadOrCreate(bepInExConfigPath);
                    
                    // [Caching]
                    ini.SetBool("Caching", "EnableAssemblyCache", enableAssemblyCache);
                    
                    // [Detours]
                    ini.SetValue("Detours", "DetourProviderType", detourProviderType ?? "Default");
                    
                    // [Harmony.Logger]
                    ini.SetValue("Harmony.Logger", "LogChannels", harmonyLogChannels ?? "Warn, Error");
                    
                    // [IL2CPP]
                    ini.SetBool("IL2CPP", "UpdateInteropAssemblies", updateInteropAssemblies);
                    ini.SetValue("IL2CPP", "UnityBaseLibrariesSource", unityBaseLibrariesSource ?? "");
                    ini.SetValue("IL2CPP", "IL2CPPInteropAssembliesPath", il2cppInteropAssembliesPath ?? "{BepInEx}");
                    ini.SetBool("IL2CPP", "PreloadIL2CPPInteropAssemblies", preloadIL2CPPInteropAssemblies);
                    
                    // [Logging]
                    ini.SetBool("Logging", "UnityLogListening", unityLogListening);
                    
                    // [Logging.Console]
                    ini.SetBool("Logging.Console", "Enabled", consoleEnabled);
                    ini.SetBool("Logging.Console", "PreventClose", consolePreventClose);
                    ini.SetBool("Logging.Console", "ShiftJisEncoding", consoleShiftJisEncoding);
                    ini.SetValue("Logging.Console", "StandardOutType", consoleStandardOutType ?? "Auto");
                    ini.SetValue("Logging.Console", "LogLevels", consoleLogLevels ?? "Fatal, Error, Warning, Message, Info");
                    
                    // [Logging.Disk]
                    ini.SetBool("Logging.Disk", "Enabled", diskLogEnabled);
                    ini.SetBool("Logging.Disk", "AppendLog", diskLogAppend);
                    ini.SetValue("Logging.Disk", "LogLevels", diskLogLevels ?? "Fatal, Error, Warning, Message, Info");
                    ini.SetBool("Logging.Disk", "InstantFlushing", diskLogInstantFlushing);
                    ini.SetInt("Logging.Disk", "ConcurrentFileLimit", diskLogConcurrentFileLimit);
                    ini.SetBool("Logging.Disk", "WriteUnityLog", writeUnityLog);
                    
                    // [Preloader]
                    ini.SetValue("Preloader", "HarmonyBackend", harmonyBackend ?? "auto");
                    ini.SetBool("Preloader", "DumpAssemblies", dumpAssemblies);
                    ini.SetBool("Preloader", "LoadDumpedAssemblies", loadDumpedAssemblies);
                    ini.SetBool("Preloader", "BreakBeforeLoadAssemblies", breakBeforeLoadAssemblies);
                    
                    // Write back to file only if there are changes
                    if (ini.HasChanges())
                    {
                        ini.Save();
                        _logger.LogInformation($"BepInEx settings saved to {bepInExConfigPath}");
                        return new JsonResult(new { success = true, message = "BepInEx设置已保存，注释已保留!" });
                    }
                    else
                    {
                        _logger.LogInformation($"No changes to save for BepInEx config {bepInExConfigPath}");
                        return new JsonResult(new { success = true, message = "BepInEx设置无更改，配置文件保持不变!" });
                    }
                }
                else
                {
                    _logger.LogWarning($"BepInEx config path not found or invalid: {bepInExConfigPath}");
                    return new JsonResult(new { success = false, message = "BepInEx配置文件路径无效或文件不存在" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving BepInEx settings: {ex.Message}");
                return new JsonResult(new { success = false, message = "BepInEx设置保存失败: " + ex.Message });
            }
        }
    }
}