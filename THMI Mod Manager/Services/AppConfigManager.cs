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

        public AppConfigManager(IWebHostEnvironment env)
        {
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

                dict[key] = value;

                if (autoSave) Save();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool RemoveKey(string section, string key, bool autoSave = true)
        {
            _lock.EnterWriteLock();
            try
            {
                section ??= string.Empty;
                if (!_data.TryGetValue(section, out var dict)) return false;
                var removed = dict.Remove(key);
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
        }

        public void Reload()
        {
            Load();
        }

        /// <summary>
        /// Get localized value for the current UI culture.
        /// Looks for a section named by full culture (e.g. "en-US"), then neutral two-letter (e.g. "en"),
        /// then a generic "Localization" section.
        /// Keys should be like "Sidebar:Home" or "Index:Welcome".
        /// </summary>
        public string? GetLocalized(string key, string? defaultValue = null)
        {
            _lock.EnterReadLock();
            try
            {
                var cultureFull = CultureInfo.CurrentUICulture.Name; // e.g. en-US
                if (!string.IsNullOrEmpty(cultureFull) && _data.TryGetValue(cultureFull, out var dict) && dict.TryGetValue(key, out var val))
                    return val;

                var neutral = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName; // e.g. en
                if (!string.IsNullOrEmpty(neutral) && _data.TryGetValue(neutral, out var dict2) && dict2.TryGetValue(key, out var val2))
                    return val2;

                if (_data.TryGetValue("Localization", out var dict3) && dict3.TryGetValue(key, out var val3))
                    return val3;

                return defaultValue;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Dispose()
        {
            _lock?.Dispose();
        }
    }
}
