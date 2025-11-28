using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Primitives;

namespace THMI_Mod_Manager.Services
{
    public class IniFileStringLocalizerFactory : IStringLocalizerFactory
    {
        private readonly string _localizationPath;
        private readonly Dictionary<string, Dictionary<string, string>> _cache = new();

        public IniFileStringLocalizerFactory(IWebHostEnvironment env)
        {
            _localizationPath = Path.Combine(env.ContentRootPath, "Localization");
            LoadAll();
        }

        private void LoadAll()
        {
            if (!Directory.Exists(_localizationPath)) return;
            foreach (var file in Directory.GetFiles(_localizationPath, "*.ini"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                // Normalize culture name: allow en_US.ini or en-US.ini -> en-US
                var cultureCandidate = fileName.Replace('_', '-');
                string cultureName;
                try
                {
                    var ci = new CultureInfo(cultureCandidate);
                    cultureName = ci.Name; // normalized name like en-US or en
                }
                catch
                {
                    // if invalid culture, skip this file
                    continue;
                }

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

                _cache[cultureName] = dict;

                // Also store neutral two-letter culture dict if not present (merge without overwriting)
                try
                {
                    var ci = new CultureInfo(cultureName);
                    var neutral = ci.TwoLetterISOLanguageName;
                    if (!_cache.ContainsKey(neutral))
                    {
                        // copy entries
                        _cache[neutral] = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch { }
            }
        }

        public IStringLocalizer Create(Type resourceSource)
        {
            return new IniFileStringLocalizer(_cache);
        }

        public IStringLocalizer Create(string baseName, string location)
        {
            return new IniFileStringLocalizer(_cache);
        }
    }

    internal class IniFileStringLocalizer : IStringLocalizer
    {
        private readonly Dictionary<string, Dictionary<string, string>> _cache;

        public IniFileStringLocalizer(Dictionary<string, Dictionary<string, string>> cache)
        {
            _cache = cache;
        }

        private string? GetValue(string name)
        {
            var culture = CultureInfo.CurrentUICulture.Name; // e.g. en-US

            // Try exact culture
            if (!string.IsNullOrEmpty(culture) && _cache.TryGetValue(culture, out var dict) && dict.TryGetValue(name, out var val))
                return val;

            // Try neutral two-letter
            var neutral = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (!string.IsNullOrEmpty(neutral) && _cache.TryGetValue(neutral, out var dict2) && dict2.TryGetValue(name, out var val2))
                return val2;

            // Try invariant (empty key)
            if (_cache.TryGetValue(string.Empty, out var dict3) && dict3.TryGetValue(name, out var val3))
                return val3;

            return null;
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
