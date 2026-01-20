using System.Collections.Concurrent;
using System.IO.Compression;
using Tommy;
using THMI_Mod_Manager.Models;

namespace THMI_Mod_Manager.Services
{
    public class ModServiceOptimized
    {
        private readonly ILogger<ModServiceOptimized> _logger;
        private readonly AppConfigManager _appConfig;
        
        // Cache for mod info data to avoid repeated file reading
        private static readonly ConcurrentDictionary<string, CachedModInfo> _modInfoCache = new();
        
        // Cache timeout (5 minutes)
        private static readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);

        // Cached mod info with timestamp
        private class CachedModInfo
        {
            public ModInfo ModInfo { get; set; } = new();
            public DateTime CacheTime { get; set; }
            public long FileSize { get; set; }
            public DateTime LastModified { get; set; }
        }

        public ModServiceOptimized(ILogger<ModServiceOptimized> logger, AppConfigManager appConfig)
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
                var dllFiles = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.TopDirectoryOnly);
                var disabledFiles = Directory.GetFiles(pluginsPath, "*.dll.disabled", SearchOption.TopDirectoryOnly);
                
                var allFiles = dllFiles.Concat(disabledFiles).ToArray();
                
                _logger.LogInformation($"Found {dllFiles.Length} DLL files and {disabledFiles.Length} disabled files in {pluginsPath}");

                foreach (var dllFile in allFiles)
                {
                    var modInfo = ExtractModInfoOptimized(dllFile);
                    mods.Add(modInfo);
                    if (modInfo.IsValid)
                    {
                        _logger.LogInformation($"Successfully loaded mod: {modInfo.Name} v{modInfo.Version} (File: {modInfo.FileName})");
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to load mod {modInfo.FileName}: {modInfo.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading mods from {pluginsPath}");
            }

            return mods;
        }

        public ModInfo ExtractModInfoOptimized(string dllPath)
        {
            var fileInfo = new FileInfo(dllPath);
            var fileSize = fileInfo.Length;
            var lastModified = fileInfo.LastWriteTime;

            var cacheKey = dllPath.ToLowerInvariant();
            if (_modInfoCache.TryGetValue(cacheKey, out var cachedInfo))
            {
                if (cachedInfo.FileSize == fileSize && cachedInfo.LastModified == lastModified && 
                    DateTime.UtcNow - cachedInfo.CacheTime < _cacheTimeout)
                {
                    _logger.LogInformation($"Using cached mod info for: {dllPath}");
                    return cachedInfo.ModInfo;
                }
                
                _modInfoCache.TryRemove(cacheKey, out _);
            }

            var modInfo = new ModInfo
            {
                FilePath = dllPath,
                FileName = Path.GetFileName(dllPath),
                FileSize = fileSize,
                LastModified = lastModified,
                InstallTime = fileInfo.CreationTime,
                IsValid = false
            };

            try
            {
                var dllFileName = Path.GetFileNameWithoutExtension(dllPath);
                var dllDirectory = Path.GetDirectoryName(dllPath) ?? string.Empty;
                var modFolder = Path.Combine(dllDirectory, dllFileName);
                
                var manifestPath = Path.Combine(modFolder, "Manifest.toml");
                
                if (File.Exists(manifestPath))
                {
                    using var reader = new StreamReader(manifestPath);
                    var manifestData = TOML.Parse(reader);
                    
                    _logger.LogInformation($"Parsed Manifest.toml from {manifestPath}");
                    _logger.LogInformation($"Manifest sections: {string.Join(", ", manifestData.Keys)}");
                    
                    if (manifestData.TryGetNode("Mod", out var modNode) && modNode is TomlTable modSection)
                    {
                        _logger.LogInformation($"Found Mod section with {modSection.ChildrenCount} keys: {string.Join(", ", modSection.Keys)}");
                        
                        if (modSection.TryGetNode("Name", out var nameNode) && nameNode is TomlString nameString)
                        {
                            modInfo.Name = nameString.Value;
                        }
                        
                        if (modSection.TryGetNode("Version", out var versionNode) && versionNode is TomlString versionString)
                        {
                            modInfo.Version = versionString.Value;
                        }
                        
                        if (modSection.TryGetNode("VersionCode", out var versionCodeNode) && versionCodeNode is TomlInteger versionCodeInt)
                        {
                            modInfo.VersionCode = (uint)versionCodeInt.Value;
                        }
                        
                        if (modSection.TryGetNode("Author", out var authorNode) && authorNode is TomlString authorString)
                        {
                            modInfo.Author = authorString.Value;
                        }
                        
                        if (modSection.TryGetNode("UniqueId", out var uniqueIdNode) && uniqueIdNode is TomlString uniqueIdString)
                        {
                            modInfo.UniqueId = uniqueIdString.Value;
                        }
                        
                        if (modSection.TryGetNode("Description", out var descriptionNode) && descriptionNode is TomlString descriptionString)
                        {
                            modInfo.Description = descriptionString.Value;
                        }
                        
                        if (modSection.TryGetNode("Link", out var linkNode) && linkNode is TomlString linkString)
                        {
                            modInfo.ModLink = linkString.Value;
                        }
                        
                        modInfo.IsValid = true;
                        _logger.LogInformation($"Successfully extracted mod info from {manifestPath}: {modInfo.Name}");
                    }
                    else
                    {
                        modInfo.ErrorMessage = "Mod section not found in Manifest.toml";
                        _logger.LogWarning($"Mod section not found in {manifestPath}. Available sections: {string.Join(", ", manifestData.Keys)}");
                    }
                }
                else
                {
                    modInfo.ErrorMessage = "Manifest.toml not found";
                    _logger.LogWarning($"Manifest.toml not found at {manifestPath}");
                    
                    var fileName = Path.GetFileNameWithoutExtension(dllPath);
                    if (fileName.EndsWith(".dll"))
                    {
                        fileName = Path.GetFileNameWithoutExtension(fileName);
                    }
                    
                    modInfo.Name = fileName;
                }
            }
            catch (Exception ex)
            {
                modInfo.ErrorMessage = $"Error reading Manifest.toml: {ex.Message}";
                _logger.LogError(ex, $"Error extracting mod info from {dllPath}");
                
                var fileName = Path.GetFileNameWithoutExtension(dllPath);
                if (fileName.EndsWith(".dll"))
                {
                    fileName = Path.GetFileNameWithoutExtension(fileName);
                }
                
                modInfo.Name = fileName;
            }

            var newCachedInfo = new CachedModInfo
            {
                ModInfo = modInfo,
                CacheTime = DateTime.UtcNow,
                FileSize = fileSize,
                LastModified = lastModified
            };
            
            _modInfoCache.AddOrUpdate(cacheKey, newCachedInfo, (key, old) => newCachedInfo);
            
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
                    var cacheKey = filePath.ToLowerInvariant();
                    _modInfoCache.TryRemove(cacheKey, out _);
                    
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

                string? fullPath = FindModFileByName(fileName);
                if (string.IsNullOrEmpty(fullPath))
                {
                    _logger.LogError($"Mod file not found by name: {fileName}");
                    return false;
                }

                var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
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
                
                var cacheKey = fullPath.ToLowerInvariant();
                _modInfoCache.TryRemove(cacheKey, out _);
                
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

        private string? FindModFileByName(string fileName)
        {
            var pluginsPath = GetPluginsPath();

            string fullPath = Path.Combine(pluginsPath, fileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }

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
                string disabledName = fileName + ".disabled";
                string disabledPath = Path.Combine(pluginsPath, disabledName);
                if (File.Exists(disabledPath))
                {
                    return disabledPath;
                }
            }

            try
            {
                var allFiles = Directory.GetFiles(pluginsPath, fileName, SearchOption.AllDirectories);
                if (allFiles.Length > 0)
                {
                    return allFiles[0];
                }

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

            return null;
        }
    }
}