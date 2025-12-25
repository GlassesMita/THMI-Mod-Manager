using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Concurrent;
using THMI_Mod_Manager.Models;

namespace THMI_Mod_Manager.Services
{
    public class ModServiceOptimized
    {
        private readonly ILogger<ModServiceOptimized> _logger;
        private readonly AppConfigManager _appConfig;
        
        // Cache for compiled delegates
        private static readonly ConcurrentDictionary<string, ModInfoDelegates> _delegateCache = new();
        
        // Cache for mod info data to avoid repeated assembly loading
        private static readonly ConcurrentDictionary<string, CachedModInfo> _modInfoCache = new();
        
        // Cache timeout (5 minutes)
        private static readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);

        // Delegates for fast field access
        private class ModInfoDelegates
        {
            public Func<object, string?>? ModNameGetter { get; set; }
            public Func<object, string?>? ModVersionGetter { get; set; }
            public Func<object, uint>? ModVersionCodeGetter { get; set; }
            public Func<object, string?>? ModAuthorGetter { get; set; }
            public Func<object, string?>? ModUniqueIdGetter { get; set; }
            public Func<object, string?>? ModDescriptionGetter { get; set; }
            public Func<object, string?>? ModLinkGetter { get; set; }
        }

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
                // Get all .dll files (including .disabled files)
                var dllFiles = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.TopDirectoryOnly);
                var disabledFiles = Directory.GetFiles(pluginsPath, "*.dll.disabled", SearchOption.TopDirectoryOnly);
                
                // Combine both lists
                var allFiles = dllFiles.Concat(disabledFiles).ToArray();
                
                _logger.LogInformation($"Found {dllFiles.Length} DLL files and {disabledFiles.Length} disabled files in {pluginsPath}");

                foreach (var dllFile in allFiles)
                {
                    var modInfo = ExtractModInfoOptimized(dllFile);
                    mods.Add(modInfo);
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

            // Check cache first
            var cacheKey = dllPath.ToLowerInvariant();
            if (_modInfoCache.TryGetValue(cacheKey, out var cachedInfo))
            {
                // Check if cache is still valid (file hasn't changed)
                if (cachedInfo.FileSize == fileSize && cachedInfo.LastModified == lastModified && 
                    DateTime.UtcNow - cachedInfo.CacheTime < _cacheTimeout)
                {
                    _logger.LogInformation($"Using cached mod info for: {dllPath}");
                    return cachedInfo.ModInfo;
                }
                
                // Cache is invalid, remove it
                _modInfoCache.TryRemove(cacheKey, out _);
            }

            var modInfo = new ModInfo
            {
                FilePath = dllPath,
                FileName = Path.GetFileName(dllPath),
                FileSize = fileSize,
                LastModified = lastModified,
                IsValid = false
            };

            try
            {
                var assembly = Assembly.LoadFrom(dllPath);
                var modInfoType = assembly.GetType("Meta.ModInfo");

                if (modInfoType != null)
                {
                    // Get or create compiled delegates for this type
                    var delegates = GetOrCreateDelegates(modInfoType);
                    
                    // Create instance (null for static fields)
                    var instance = Activator.CreateInstance(modInfoType, true);

                    // Use compiled delegates for fast field access
                    modInfo.Name = delegates.ModNameGetter?.Invoke(instance ?? new object()) ?? string.Empty;
                    modInfo.Version = delegates.ModVersionGetter?.Invoke(instance ?? new object()) ?? string.Empty;
                    
                    if (delegates.ModVersionCodeGetter != null)
                    {
                        modInfo.VersionCode = delegates.ModVersionCodeGetter.Invoke(instance ?? new object());
                    }
                    
                    modInfo.Author = delegates.ModAuthorGetter?.Invoke(instance ?? new object()) ?? string.Empty;
                    modInfo.UniqueId = delegates.ModUniqueIdGetter?.Invoke(instance ?? new object()) ?? string.Empty;
                    modInfo.Description = delegates.ModDescriptionGetter?.Invoke(instance ?? new object()) ?? string.Empty;
                    
                    var linkValue = delegates.ModLinkGetter?.Invoke(instance ?? new object());
                    if (!string.IsNullOrWhiteSpace(linkValue))
                    {
                        modInfo.ModLink = linkValue;
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
                modInfo.ErrorMessage = $"Error loading DLL: {ex.Message}";
                _logger.LogWarning($"Could not load assembly from {dllPath}, attempting to extract basic info: {ex.Message}");
                
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

            // Cache the result
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

        private ModInfoDelegates GetOrCreateDelegates(Type modInfoType)
        {
            var typeName = modInfoType.AssemblyQualifiedName ?? modInfoType.FullName ?? modInfoType.Name;
            
            return _delegateCache.GetOrAdd(typeName, key =>
            {
                _logger.LogInformation($"Creating compiled delegates for type: {typeName}");
                
                var delegates = new ModInfoDelegates();
                
                // Compile delegates for each field
                delegates.ModNameGetter = CreateFieldGetter(modInfoType, "ModName");
                delegates.ModVersionGetter = CreateFieldGetter(modInfoType, "ModVersion");
                delegates.ModVersionCodeGetter = CreateFieldGetter<uint>(modInfoType, "ModVersionCode");
                delegates.ModAuthorGetter = CreateFieldGetter(modInfoType, "ModAuthor");
                delegates.ModUniqueIdGetter = CreateFieldGetter(modInfoType, "ModUniqueId");
                delegates.ModDescriptionGetter = CreateFieldGetter(modInfoType, "ModDescription");
                delegates.ModLinkGetter = CreateFieldGetter(modInfoType, "ModLink");
                
                return delegates;
            });
        }

        private Func<object, string?>? CreateFieldGetter(Type type, string fieldName)
        {
            try
            {
                var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
                if (field == null) return null;

                // Create parameter expression for the instance (even though it's static, we need it for the delegate signature)
                var instanceParam = Expression.Parameter(typeof(object), "instance");
                
                // Create field access expression
                var fieldAccess = Expression.Field(null, field); // null for static fields
                
                // Convert to string if necessary
                Expression result = fieldAccess;
                if (field.FieldType != typeof(string))
                {
                    result = Expression.Call(fieldAccess, "ToString", Type.EmptyTypes);
                }
                
                // Create lambda expression
                var lambda = Expression.Lambda<Func<object, string?>>(result, instanceParam);
                
                // Compile to delegate
                return lambda.Compile();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not create getter for field {fieldName} in type {type.Name}: {ex.Message}");
                return null;
            }
        }

        private Func<object, T>? CreateFieldGetter<T>(Type type, string fieldName)
        {
            try
            {
                var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
                if (field == null) return null;

                var instanceParam = Expression.Parameter(typeof(object), "instance");
                var fieldAccess = Expression.Field(null, field); // null for static fields
                
                // Convert to target type if necessary
                Expression result = fieldAccess;
                if (field.FieldType != typeof(T))
                {
                    result = Expression.Convert(fieldAccess, typeof(T));
                }
                
                var lambda = Expression.Lambda<Func<object, T>>(result, instanceParam);
                return lambda.Compile();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not create typed getter for field {fieldName} in type {type.Name}: {ex.Message}");
                return null;
            }
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
                    // Remove from cache when deleting
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

                string fullPath = FindModFileByName(fileName);
                if (fullPath == null)
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
                
                // Remove from cache when toggling
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