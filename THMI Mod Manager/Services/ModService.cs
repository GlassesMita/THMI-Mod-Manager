using System.Reflection;
using THMI_Mod_Manager.Models;

namespace THMI_Mod_Manager.Services
{
    public class ModService
    {
        private readonly ILogger<ModService> _logger;
        private readonly AppConfigManager _appConfig;

        public ModService(ILogger<ModService> logger, AppConfigManager appConfig)
        {
            _logger = logger;
            _appConfig = appConfig;
        }

        public List<ModInfo> LoadMods()
        {
            var mods = new List<ModInfo>();
            var pluginsPath = GetPluginsPath();

            if (!Directory.Exists(pluginsPath))
            {
                _logger.LogWarning($"BepInEx/plugins directory not found: {pluginsPath}");
                return mods;
            }

            try
            {
                // Get all .dll files (including .disabled files)
                var dllFiles = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.TopDirectoryOnly);
                var disabledFiles = Directory.GetFiles(pluginsPath, "*.dll.disabled", SearchOption.TopDirectoryOnly);
                
                // Combine both lists
                var allFiles = dllFiles.Concat(disabledFiles).ToArray();
                
                _logger.LogInformation($"Found {dllFiles.Length} DLL files and {disabledFiles.Length} disabled files in {pluginsPath}");

                foreach (var dllFile in allFiles)
                {
                    var modInfo = ExtractModInfo(dllFile);
                    mods.Add(modInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading mods from {pluginsPath}");
            }

            return mods;
        }

        public ModInfo ExtractModInfo(string dllPath)
        {
            var modInfo = new ModInfo
            {
                FilePath = dllPath,
                FileName = Path.GetFileName(dllPath),
                FileSize = new FileInfo(dllPath).Length,
                LastModified = File.GetLastWriteTime(dllPath),
                IsValid = false
            };

            try
            {
                // If the file is a .disabled file, we need to temporarily rename it to load the assembly
                string tempPath = null;
                string originalPath = dllPath;
                
                if (dllPath.EndsWith(".disabled"))
                {
                    // Create a temporary path without the .disabled extension to load the assembly
                    string enabledPath = dllPath.Substring(0, dllPath.Length - ".disabled".Length);
                    string tempDir = Path.GetDirectoryName(dllPath);
                    string tempFileName = Path.GetFileName(enabledPath);
                    tempPath = Path.Combine(tempDir, tempFileName);
                }

                // For .disabled files, we'll copy to temp location to read metadata
                string assemblyPath = dllPath;
                
                // For .disabled files, we need to temporarily make a copy to read metadata
                if (dllPath.EndsWith(".disabled"))
                {
                    // For .disabled files, we'll try to load metadata directly from the .disabled file
                    // .NET can load assemblies with any extension
                }

                var assembly = Assembly.LoadFrom(dllPath);
                var modInfoType = assembly.GetType("Meta.ModInfo");

                if (modInfoType != null)
                {
                    var modNameField = modInfoType.GetField("ModName", BindingFlags.Public | BindingFlags.Static);
                    var modVersionField = modInfoType.GetField("ModVersion", BindingFlags.Public | BindingFlags.Static);
                    var modVersionCodeField = modInfoType.GetField("ModVersionCode", BindingFlags.Public | BindingFlags.Static);
                    var modAuthorField = modInfoType.GetField("ModAuthor", BindingFlags.Public | BindingFlags.Static);
                    var modUniqueIdField = modInfoType.GetField("ModUniqueId", BindingFlags.Public | BindingFlags.Static);
                    var modDescriptionField = modInfoType.GetField("ModDescription", BindingFlags.Public | BindingFlags.Static);
                    var modLinkField = modInfoType.GetField("ModLink", BindingFlags.Public | BindingFlags.Static);

                    if (modNameField != null)
                    {
                        modInfo.Name = modNameField.GetValue(null)?.ToString() ?? string.Empty;
                    }

                    if (modVersionField != null)
                    {
                        modInfo.Version = modVersionField.GetValue(null)?.ToString() ?? string.Empty;
                    }

                    if (modVersionCodeField != null)
                    {
                        var versionCodeValue = modVersionCodeField.GetValue(null);
                        if (versionCodeValue is uint code)
                        {
                            modInfo.VersionCode = code;
                        }
                    }

                    if (modAuthorField != null)
                    {
                        modInfo.Author = modAuthorField.GetValue(null)?.ToString() ?? string.Empty;
                    }

                    if (modUniqueIdField != null)
                    {
                        modInfo.UniqueId = modUniqueIdField.GetValue(null)?.ToString() ?? string.Empty;
                    }

                    if (modDescriptionField != null)
                    {
                        modInfo.Description = modDescriptionField.GetValue(null)?.ToString() ?? string.Empty;
                    }

                    if (modLinkField != null)
                    {
                        var linkValue = modLinkField.GetValue(null)?.ToString();
                        if (!string.IsNullOrWhiteSpace(linkValue))
                        {
                            modInfo.ModLink = linkValue;
                        }
                    }

                    modInfo.IsValid = true;
                    _logger.LogInformation($"Successfully extracted mod info from {dllPath}: {modInfo.Name}");
                }
                else
                {
                    modInfo.ErrorMessage = "Meta.ModInfo class not found";
                    _logger.LogWarning($"Meta.ModInfo class not found in {dllPath}");
                }
            }
            catch (FileLoadException ex)
            {
                // This might happen with .disabled files due to their extension
                modInfo.ErrorMessage = $"Error loading DLL: {ex.Message}";
                _logger.LogWarning($"Could not load assembly from {dllPath}, attempting to extract basic info: {ex.Message}");
                
                // Try to extract basic info from the filename
                var fileName = Path.GetFileNameWithoutExtension(dllPath);
                if (fileName.EndsWith(".dll"))
                {
                    fileName = Path.GetFileNameWithoutExtension(fileName);
                }
                
                modInfo.Name = fileName;
                modInfo.IsValid = false;
            }
            catch (Exception ex)
            {
                modInfo.ErrorMessage = $"Error reading DLL: {ex.Message}";
                _logger.LogError(ex, $"Error extracting mod info from {dllPath}");
            }

            return modInfo;
        }

        private string GetPluginsPath()
        {
            var gamePath = _appConfig.Get("[Game]GamePath", "");
            
            if (!string.IsNullOrEmpty(gamePath))
            {
                var pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
                if (Directory.Exists(pluginsPath))
                {
                    _logger.LogInformation($"Using game plugins path: {pluginsPath}");
                    return pluginsPath;
                }
            }

            var possiblePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "BepInEx", "plugins"),
                Path.Combine(Directory.GetCurrentDirectory(), "BepInEx", "plugins"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "BepInEx", "plugins"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "BepInEx", "plugins"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "BepInEx", "plugins"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "BepInEx", "plugins")
            };

            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                {
                    _logger.LogInformation($"Using workspace plugins path: {fullPath}");
                    return fullPath;
                }
            }
            
            _logger.LogWarning("BepInEx/plugins directory not found in any location");
            return possiblePaths[0];
        }

        public bool DeleteMod(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation($"Successfully deleted mod: {filePath}");
                    return true;
                }
                _logger.LogWarning($"Mod file not found: {filePath}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting mod: {filePath}");
                return false;
            }
        }

        public bool ToggleMod(string fileName)
        {
            try
            {
                _logger.LogInformation($"Attempting to toggle mod: {fileName}");
                
                if (string.IsNullOrEmpty(fileName))
                {
                    _logger.LogError("File name is null or empty");
                    return false;
                }

                string fullPath = FindModFileByName(fileName);
                if (fullPath == null)
                {
                    _logger.LogError($"Mod file not found by name: {fileName}");
                    return false;
                }

                var directory = Path.GetDirectoryName(fullPath);
                var fileNameOnly = Path.GetFileName(fullPath);
                string newFilePath;

                if (fileNameOnly.EndsWith(".disabled"))
                {
                    newFilePath = Path.Combine(directory, fileNameOnly.Substring(0, fileNameOnly.Length - ".disabled".Length));
                    _logger.LogInformation($"Enabling mod: {fullPath} -> {newFilePath}");
                }
                else
                {
                    newFilePath = Path.Combine(directory, fileNameOnly + ".disabled");
                    _logger.LogInformation($"Disabling mod: {fullPath} -> {newFilePath}");
                }

                File.Move(fullPath, newFilePath);
                _logger.LogInformation($"Successfully toggled mod: {newFilePath}");
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, $"Access denied when toggling mod: {fileName}");
                return false;
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError(ex, $"Directory not found when toggling mod: {fileName}");
                return false;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, $"IO error when toggling mod: {fileName}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error toggling mod: {fileName}");
                return false;
            }
        }

        private string FindModFileByName(string fileName)
        {
            var pluginsPath = GetPluginsPath();

            // First, try to find the exact file name in the plugins directory
            string fullPath = Path.Combine(pluginsPath, fileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            // If not found, check if it's a disabled file and try to find the enabled version
            if (fileName.EndsWith(".disabled"))
            {
                string enabledName = fileName.Substring(0, fileName.Length - ".disabled".Length);
                string enabledPath = Path.Combine(pluginsPath, enabledName);
                if (File.Exists(enabledPath))
                {
                    return enabledPath;
                }
            }
            else
            {
                // If it's an enabled file, try to find the disabled version
                string disabledName = fileName + ".disabled";
                string disabledPath = Path.Combine(pluginsPath, disabledName);
                if (File.Exists(disabledPath))
                {
                    return disabledPath;
                }
            }

            // If not found in plugins directory, search in subdirectories
            try
            {
                var allFiles = Directory.GetFiles(pluginsPath, fileName, SearchOption.AllDirectories);
                if (allFiles.Length > 0)
                {
                    return allFiles[0]; // Return the first match
                }

                // If still not found, try searching for both enabled and disabled versions
                if (fileName.EndsWith(".disabled"))
                {
                    var enabledFiles = Directory.GetFiles(pluginsPath, fileName.Substring(0, fileName.Length - ".disabled".Length), SearchOption.TopDirectoryOnly);
                    if (enabledFiles.Length > 0)
                    {
                        return enabledFiles[0];
                    }
                }
                else
                {
                    var disabledFiles = Directory.GetFiles(pluginsPath, fileName + ".disabled", SearchOption.TopDirectoryOnly);
                    if (disabledFiles.Length > 0)
                    {
                        return disabledFiles[0];
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching for mod file: {fileName}");
            }

            return null; // File not found
        }
    }
}
