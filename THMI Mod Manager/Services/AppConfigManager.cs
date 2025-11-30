using System.Text;
using System.Globalization;
using System.Linq;

namespace THMI_Mod_Manager.Services
{
    /// <summary>
    /// Simple INI-style application configuration manager.
    /// Saves to a file named `AppConfig.Schale` located at the application's content root.
    /// Thread-safe for concurrent reads/writes.
    /// </summary>
    public class AppConfigManager : IDisposable
    {
        private readonly string _filePath;
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly Dictionary<string, Dictionary<string, string>> _data = new(StringComparer.OrdinalIgnoreCase);

        private readonly IConfiguration _config;
    private readonly ILogger<AppConfigManager> _logger;
    private readonly IServiceProvider _serviceProvider;

    public AppConfigManager(IWebHostEnvironment env, IConfiguration configuration, ILogger<AppConfigManager> logger, IServiceProvider serviceProvider)
    {
        _config = configuration;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _filePath = Path.Combine(env.ContentRootPath, "AppConfig.Schale");
        Load();
    }

        private void Load()
        {
            _lock.EnterWriteLock();
            try
            {
                _data.Clear();
                if (!File.Exists(_filePath))
                {
                    // ensure directory exists (content root should exist) and create empty file
                    File.WriteAllText(_filePath, string.Empty, Encoding.UTF8);
                    
                    // Log that new config file was created
                    var logMessage = $"AppConfig.Schale created at: {_filePath}";
                    Logger.Log(Logger.LogLevel.Info, logMessage);
                    
                    // Also log to Microsoft logger if available
                    if (_logger != null)
                    {
                        _logger.LogInformation(logMessage);
                    }
                    
                    return;
                }

                string? currentSection = string.Empty;
                foreach (var raw in File.ReadAllLines(_filePath, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("#") || line.StartsWith(";")) continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        if (!_data.ContainsKey(currentSection))
                            _data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        continue;
                    }

                    var idx = line.IndexOf('=');
                    if (idx <= 0)
                        continue;

                    var key = line.Substring(0, idx).Trim();
                    var value = line.Substring(idx + 1).Trim();

                    if (!_data.ContainsKey(currentSection))
                        _data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    _data[currentSection][key] = value;
                }
                
                // Log successful config load
                var loadLogMessage = $"AppConfig.Schale loaded from: {_filePath} ({_data.Count} sections)";
                Logger.Log(Logger.LogLevel.Info, loadLogMessage);
                
                // Also log to Microsoft logger if available
                if (_logger != null)
                {
                    _logger.LogInformation(loadLogMessage);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public string? Get(string section, string key, string? defaultValue = null)
        {
            _lock.EnterReadLock();
            try
            {
                section ??= string.Empty;
                if (_data.TryGetValue(section, out var dict) && dict.TryGetValue(key, out var val))
                    return val;

                return defaultValue;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        public string? Get(string key, string? defaultValue = null)
        {
            // Parse key in format [Section]Key or just Key
            if (key.StartsWith("[") && key.Contains("]"))
            {
                var endBracket = key.IndexOf(']');
                var section = key.Substring(1, endBracket - 1);
                var actualKey = key.Substring(endBracket + 1);
                return Get(section, actualKey, defaultValue);
            }
            
            // Try to find the key in any section
            _lock.EnterReadLock();
            try
            {
                foreach (var section in _data.Values)
                {
                    if (section.TryGetValue(key, out var value))
                    {
                        return value;
                    }
                }
                return defaultValue;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public IReadOnlyDictionary<string, string> GetSection(string section)
        {
            _lock.EnterReadLock();
            try
            {
                section ??= string.Empty;
                if (_data.TryGetValue(section, out var dict))
                    return new Dictionary<string, string>(dict);

                return new Dictionary<string, string>();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public List<string> GetAllSections()
        {
            _lock.EnterReadLock();
            try
            {
                return new List<string>(_data.Keys);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public List<string> GetSectionKeys(string section)
        {
            _lock.EnterReadLock();
            try
            {
                section ??= string.Empty;
                return _data.TryGetValue(section, out var dict) 
                    ? new List<string>(dict.Keys) 
                    : new List<string>();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Set(string section, string key, string value, bool autoSave = true)
        {
            _lock.EnterWriteLock();
            try
            {
                section ??= string.Empty;
                if (!_data.TryGetValue(section, out var dict))
                {
                    dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _data[section] = dict;
                }

                // Log the configuration change
                var oldValue = dict.ContainsKey(key) ? dict[key] : null;
                if (oldValue != value)
                {
                    dict[key] = value;
                    
                    // Log to custom logger
                    var logMessage = $"Config updated: [{section}]{key} = {value}";
                    if (!string.IsNullOrEmpty(oldValue))
                        logMessage += $" (was: {oldValue})";
                    
                    Logger.Log(Logger.LogLevel.Info, logMessage);
                    
                    // Also log to Microsoft logger if available
                    if (_logger != null)
                    {
                        _logger.LogInformation(logMessage);
                    }
                }

                if (autoSave) Save();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        public void Set(string key, string value, bool autoSave = true)
        {
            // Parse key in format [Section]Key or just Key
            if (key.StartsWith("[") && key.Contains("]"))
            {
                var endBracket = key.IndexOf(']');
                var section = key.Substring(1, endBracket - 1);
                var actualKey = key.Substring(endBracket + 1);
                Set(section, actualKey, value, autoSave);
                return;
            }
            
            // If no section specified, use empty section
            Set(string.Empty, key, value, autoSave);
        }

        public bool RemoveKey(string section, string key, bool autoSave = true)
        {
            _lock.EnterWriteLock();
            try
            {
                section ??= string.Empty;
                if (!_data.TryGetValue(section, out var dict)) return false;
                var removed = dict.Remove(key);
                
                if (removed)
                {
                    // Log the configuration removal
                    var logMessage = $"Config removed: [{section}]{key}";
                    Logger.Log(Logger.LogLevel.Info, logMessage);
                    
                    // Also log to Microsoft logger if available
                    if (_logger != null)
                    {
                        _logger.LogInformation(logMessage);
                    }
                }
                
                if (autoSave && removed) Save();
                return removed;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool RemoveSection(string section, bool autoSave = true)
        {
            _lock.EnterWriteLock();
            try
            {
                section ??= string.Empty;
                var removed = _data.Remove(section);
                
                if (removed)
                {
                    // Log the section removal
                    var logMessage = $"Config section removed: [{section}]";
                    Logger.Log(Logger.LogLevel.Info, logMessage);
                    
                    // Also log to Microsoft logger if available
                    if (_logger != null)
                    {
                        _logger.LogInformation(logMessage);
                    }
                }
                
                if (autoSave && removed) Save();
                return removed;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Save()
        {
            // Avoid acquiring locks that cause recursion when Save is called while a write lock is already held.
            // If current thread already has the write lock, write directly from _data. Otherwise snapshot under read lock and write.
            if (_lock.IsWriteLockHeld)
            {
                WriteToFile(_data);
                return;
            }

            Dictionary<string, Dictionary<string, string>> snapshot;
            _lock.EnterReadLock();
            try
            {
                snapshot = _data.ToDictionary(
                    s => s.Key,
                    s => s.Value.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                _lock.ExitReadLock();
            }

            WriteToFile(snapshot);
        }

        private void WriteToFile(Dictionary<string, Dictionary<string, string>> source)
        {
            var sb = new StringBuilder();
            foreach (var sectionKvp in source)
            {
                var section = sectionKvp.Key ?? string.Empty;
                if (!string.IsNullOrEmpty(section))
                {
                    sb.AppendLine($"[{section}]");
                }

                foreach (var kv in sectionKvp.Value)
                {
                    sb.AppendLine($"{kv.Key}={kv.Value}");
                }

                sb.AppendLine();
            }

            File.WriteAllText(_filePath, sb.ToString(), Encoding.UTF8);
            
            // Log that AppConfig.Schale was written
            var logMessage = $"AppConfig.Schale written to: {_filePath}";
            Logger.Log(Logger.LogLevel.Info, logMessage);
            
            // Also log to Microsoft logger if available
            if (_logger != null)
            {
                _logger?.LogInformation(logMessage);
            }
        }

        public void Reload()
        {
            Load();
        }

        /// <summary>
        /// Get localized value for the configured language.
        /// Looks for localization files in Localization or Resources directories.
        /// Supports multiple fallback strategies: exact match, normalized format, neutral culture, default culture.
        /// </summary>
        public string GetLocalized(string key, string? defaultValue = null)
    {
        // Get the configured language from the config file
        var language = Get("[Localization]Language", "en_US");
        
        // Get the base path for localization files
        var basePath = Path.Combine(Path.GetDirectoryName(_filePath) ?? "", "Localization");
        if (!Directory.Exists(basePath))
        {
            basePath = Path.Combine(Path.GetDirectoryName(_filePath) ?? "", "Resources");
        }
        
        // If no localization directory exists, fallback to config-based approach
        if (!Directory.Exists(basePath))
        {
            _lock.EnterReadLock();
            try
            {
                // Convert to standard format (e.g., en-US)
                var cultureName = language.Replace('_', '-');
                
                // First try with the full culture name (e.g., "en-US")
                if (!string.IsNullOrEmpty(cultureName) && _data.TryGetValue(cultureName, out var dict) && dict.TryGetValue(key, out var val))
                    return val;

                // Then try with the neutral culture (e.g., "en")
                var neutral = cultureName.Split('-')[0];
                if (!string.IsNullOrEmpty(neutral) && _data.TryGetValue(neutral, out var dict2) && dict2.TryGetValue(key, out var val2))
                    return val2;

                // Finally try with the generic "Localization" section
                if (_data.TryGetValue("Localization", out var dict3) && dict3.TryGetValue(key, out var val3))
                    return val3;

                return defaultValue ?? key;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        // Load localization resources from files
        var cache = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var localizationFiles = Directory.GetFiles(basePath, "*.ini");
        
        foreach (var file in localizationFiles)
        {
            var cultureName = Path.GetFileNameWithoutExtension(file);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? currentSection = null;
            
            foreach (var rawLine in File.ReadAllLines(file, Encoding.UTF8))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
                    continue;
                
                // Section header [SectionName]
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                    
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                    
                var k = line.Substring(0, idx).Trim();
                var v = line.Substring(idx + 1).Trim();
                
                // Store with section prefix if in a section
                var storeKey = string.IsNullOrEmpty(currentSection) ? k : $"{currentSection}:{k}";
                dict[storeKey] = v;
            }
            
            cache[cultureName] = dict;
        }
        
        // Try to get localized value with multiple fallback strategies
        // 1. Try exact match (e.g., zh_CN)
        if (cache.TryGetValue(language, out var dict1) && dict1.TryGetValue(key, out var value1))
            return value1;
            
        // 2. Try normalized format (e.g., zh-CN)
        var normalizedLanguage = language.Replace('_', '-');
        if (normalizedLanguage != language && cache.TryGetValue(normalizedLanguage, out var dictNormalized) && dictNormalized.TryGetValue(key, out var valueNormalized))
            return valueNormalized;
            
        // 3. Try neutral culture (e.g., zh)
        if (normalizedLanguage.Contains('-'))
        {
            var neutralLanguage = normalizedLanguage.Split('-')[0];
            if (cache.TryGetValue(neutralLanguage, out var dict3) && dict3.TryGetValue(key, out var value3))
                return value3;
        }
            
        // 4. Try default culture (en)
        if (cache.TryGetValue("en", out var dict4) && dict4.TryGetValue(key, out var value4))
            return value4;
            
        // 5. Try empty culture
        if (cache.TryGetValue(string.Empty, out var dict5) && dict5.TryGetValue(key!, out var value5))
            return value5;
            
        // 6. Return default value or key as fallback
        return defaultValue ?? key;
    }

        public void Dispose()
        {
            _lock?.Dispose();
        }
    }
}