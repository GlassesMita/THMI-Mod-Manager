using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Primitives;

namespace THMI_Mod_Manager.Services
{
    public interface ICultureAwareStringLocalizer
    {
        void SetCulture(string culture);
    }

    public class IniFileStringLocalizerFactory : IStringLocalizerFactory
    {
        private readonly string _localizationPath;
        private readonly Dictionary<string, Dictionary<string, string>> _cache = new();

        public IniFileStringLocalizerFactory(IWebHostEnvironment env, IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            _localizationPath = Path.Combine(env.ContentRootPath, "Localization");
            LoadAll();
        }

        private void LoadAll()
        {
            if (!Directory.Exists(_localizationPath)) return;
            foreach (var file in Directory.GetFiles(_localizationPath, "*.ini"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                // Keep the original filename (e.g., en_US) for direct lookup
                var cultureName = fileName;
                
                // Also store normalized version (e.g., en-US) for compatibility
                var normalizedCulture = fileName.Replace('_', '-');
                
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string? currentSection = null;
                foreach (var rawLine in File.ReadAllLines(file))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("#") || line.StartsWith(";")) continue;

                    // Section header [SectionName]
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    var key = line.Substring(0, idx).Trim();
                    var value = line.Substring(idx + 1).Trim();

                    var storeKey = string.IsNullOrEmpty(currentSection) ? key : $"{currentSection}:{key}";
                    dict[storeKey] = value;
                }

                // Store with both original filename and normalized version
                _cache[cultureName] = dict;
                if (cultureName != normalizedCulture)
                {
                    _cache[normalizedCulture] = dict;
                }

                // Also store neutral two-letter culture dict if not present
                var neutral = normalizedCulture.Split('-')[0];
                if (!string.IsNullOrEmpty(neutral) && !_cache.ContainsKey(neutral))
                {
                    // copy entries
                    _cache[neutral] = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
                }
            }
            
            // Also load TOML files
            foreach (var file in Directory.GetFiles(_localizationPath, "*.toml"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var cultureName = fileName;
                
                // Also store normalized version (e.g., en-US) for compatibility
                var normalizedCulture = fileName.Replace('_', '-');
                
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string? currentSection = null;
                foreach (var rawLine in File.ReadAllLines(file))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("#") || line.StartsWith(";")) continue;
                    
                    // TOML section header [SectionName]
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    // TOML key = "value" format
                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    var key = line.Substring(0, idx).Trim();
                    var value = line.Substring(idx + 1).Trim();

                    var storeKey = string.IsNullOrEmpty(currentSection) ? key : $"{currentSection}:{key}";
                    dict[storeKey] = value;
                }
                // Store with both original filename and normalized version
                _cache[cultureName] = dict;
                if (cultureName != normalizedCulture)
                {
                    _cache[normalizedCulture] = dict;
                }
                // Also store neutral two-letter culture dict if not present
                var neutral = normalizedCulture.Split('-')[0];
                if (!string.IsNullOrEmpty(neutral) && !_cache.ContainsKey(neutral))
                {
                    // copy entries
                    _cache[neutral] = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public IStringLocalizer Create(Type resourceSource)
        {
            var localizer = new IniFileStringLocalizer(_cache);
            
            // Get the configured language from AppConfigManager
            var env = (IWebHostEnvironment?)serviceProvider.GetService(typeof(IWebHostEnvironment));
            if (env == null)
                return localizer;
            
            var configPath = Path.Combine(env.ContentRootPath, "AppConfig.Schale");
            if (File.Exists(configPath))
            {
                var language = "en_US"; // default
                string? currentSection = null;
                foreach (var rawLine in File.ReadAllLines(configPath))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("#") || line.StartsWith(";")) continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    var key = line.Substring(0, idx).Trim();
                    var value = line.Substring(idx + 1).Trim();

                    if (string.Equals(currentSection, "Localization", StringComparison.OrdinalIgnoreCase) && 
                        string.Equals(key, "Language", StringComparison.OrdinalIgnoreCase))
                    {
                        language = value;
                        break;
                    }
                }
                
                localizer.SetCulture(language);
            }
            
            return localizer;
        }

        public IStringLocalizer Create(string baseName, string location)
        {
            return Create(typeof(object));
        }
        
        private readonly IServiceProvider serviceProvider;
    }

    internal sealed class IniFileStringLocalizer : IStringLocalizer, ICultureAwareStringLocalizer
    {
        private readonly Dictionary<string, Dictionary<string, string>> _cache;

        public IniFileStringLocalizer(Dictionary<string, Dictionary<string, string>> cache)
        {
            _cache = cache;
        }

        private string? GetValue(string name)
        {
            // Try exact culture
            if (!string.IsNullOrEmpty(_currentCulture) && _cache.TryGetValue(_currentCulture, out var dict) && dict.TryGetValue(name, out var val))
                return val;

            // Try normalized culture (e.g., en-US instead of en_US)
            if (!string.IsNullOrEmpty(_currentCulture))
            {
                var normalizedCulture = _currentCulture.Replace('_', '-');
                if (normalizedCulture != _currentCulture && _cache.TryGetValue(normalizedCulture, out var dict2) && dict2.TryGetValue(name, out var val2))
                    return val2;
            }

            // Try neutral two-letter
            var neutral = !string.IsNullOrEmpty(_currentCulture) ? _currentCulture.Replace('_', '-').Split('-')[0] : "en";
            if (!string.IsNullOrEmpty(neutral) && _cache.TryGetValue(neutral, out var dict3) && dict3.TryGetValue(name, out var val3))
                return val3;

            // Try invariant (empty key)
            if (_cache.TryGetValue(string.Empty, out var dict4) && dict4.TryGetValue(name, out var val4))
                return val4;

            return null;
        }
        
        private string _currentCulture = CultureInfo.CurrentUICulture.Name;
        
        public void SetCulture(string culture)
        {
            _currentCulture = culture;
        }

        public LocalizedString this[string name]
        {
            get
            {
                var val = GetValue(name);
                return new LocalizedString(name, val ?? name, resourceNotFound: val == null);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var format = GetValue(name) ?? name;
                var value = string.Format(format, arguments);
                return new LocalizedString(name, value, resourceNotFound: format == name);
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            if (!string.IsNullOrEmpty(culture) && _cache.TryGetValue(culture, out var dict))
            {
                foreach (var kv in dict)
                    yield return new LocalizedString(kv.Key, kv.Value, resourceNotFound: false);
            }
        }

        public IStringLocalizer WithCulture(CultureInfo culture)
        {
            return this; // simple implementation
        }
    }
}