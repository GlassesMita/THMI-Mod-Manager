using System.Globalization;

namespace THMI_Mod_Manager.Services
{
    public record LocaleInfo(string FileName, string CultureName, string FriendlyName);

    public class LocalizationManager
    {
        private readonly string _localizationPath;
        private readonly List<LocaleInfo> _locales = new();

        public LocalizationManager(IWebHostEnvironment env)
        {
            _localizationPath = Path.Combine(env.ContentRootPath, "Localization");
            Load();
        }

        private void Load()
        {
            _locales.Clear();
            if (!Directory.Exists(_localizationPath)) return;

            foreach (var file in Directory.GetFiles(_localizationPath, "*.ini"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file); // e.g. en_US or en-US
                var candidate = fileName; // keep original for saving
                var normalized = candidate.Replace('_', '-');
                string cultureName;
                try
                {
                    var ci = new CultureInfo(normalized);
                    cultureName = ci.Name; // e.g. en-US or en
                }
                catch
                {
                    cultureName = normalized; // fallback
                }

                // Attempt to read FriendlyName from INI: [Meta]FriendlyName or [App]Name
                string friendly = string.Empty;
                string? currentSection = null;
                foreach (var raw in File.ReadAllLines(file))
                {
                    var line = raw.Trim();
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

                    if (string.Equals(currentSection, "Lang", StringComparison.OrdinalIgnoreCase) && string.Equals(key, "Name", StringComparison.OrdinalIgnoreCase))
                    {
                        friendly = value;
                        break;
                    }

                    if (string.IsNullOrEmpty(friendly) && string.Equals(currentSection, "App", StringComparison.OrdinalIgnoreCase) && string.Equals(key, "Name", StringComparison.OrdinalIgnoreCase))
                    {
                        friendly = value; // tentative
                    }
                }

                if (string.IsNullOrEmpty(friendly))
                {
                    try
                    {
                        var ci = new CultureInfo(normalized);
                        friendly = ci.NativeName ?? ci.DisplayName ?? normalized;
                    }
                    catch
                    {
                        friendly = candidate;
                    }
                }

                _locales.Add(new LocaleInfo(candidate, cultureName, friendly));
            }
        }

        public IReadOnlyList<LocaleInfo> GetAvailableLocales() => _locales.AsReadOnly();
    }
}
