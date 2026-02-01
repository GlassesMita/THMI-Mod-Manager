using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace THMI_Mod_Manager.Services
{
    public class IniFileHelper
    {
        private readonly string _filePath;
        private readonly List<IniLine> _lines = new();
        private readonly Dictionary<string, Dictionary<string, string>> _data = new();
        private readonly HashSet<string> _modifiedKeys = new(StringComparer.OrdinalIgnoreCase);

        private class IniLine
        {
            public string Original { get; set; } = "";
            public string? Section { get; set; }
            public string? Key { get; set; }
            public bool IsComment { get; set; }
            public bool IsEmpty { get; set; }
            public bool IsSection { get; set; }
            public bool IsModified { get; set; }
        }

        public IniFileHelper(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        public void Load()
        {
            _lines.Clear();
            _data.Clear();

            if (!File.Exists(_filePath))
                return;

            string? currentSection = null;
            var allLines = File.ReadAllLines(_filePath);

            foreach (var rawLine in allLines)
            {
                var line = new IniLine { Original = rawLine };
                var trimmed = rawLine.Trim();

                if (string.IsNullOrEmpty(trimmed))
                {
                    line.IsEmpty = true;
                    _lines.Add(line);
                    continue;
                }

                if (trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                {
                    line.IsComment = true;
                    line.Section = currentSection;
                    _lines.Add(line);
                    continue;
                }

                var sectionMatch = Regex.Match(trimmed, @"^\[(.+)\]$");
                if (sectionMatch.Success)
                {
                    currentSection = sectionMatch.Groups[1].Value.Trim();
                    line.IsSection = true;
                    line.Section = currentSection;
                    _lines.Add(line);

                    if (!_data.ContainsKey(currentSection))
                    {
                        _data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                    continue;
                }

                var idx = trimmed.IndexOf('=');
                if (idx > 0)
                {
                    var key = trimmed.Substring(0, idx).Trim();
                    var value = trimmed.Substring(idx + 1).Trim();

                    line.Section = currentSection;
                    line.Key = key;

                    if (currentSection != null)
                    {
                        _data[currentSection][key] = value;
                    }

                    _lines.Add(line);
                }
            }
        }

        public void Save()
        {
            var sb = new StringBuilder();
            string? currentSection = null;

            foreach (var line in _lines)
            {
                if (line.IsSection)
                {
                    currentSection = line.Section;
                    sb.AppendLine(line.Original);
                }
                else if (line.IsComment || line.IsEmpty)
                {
                    sb.AppendLine(line.Original);
                }
                else if (line.Key != null && line.Section != null)
                {
                    if (line.IsModified && _data.TryGetValue(line.Section, out var sectionData) &&
                        sectionData.TryGetValue(line.Key, out var newValue))
                    {
                        sb.AppendLine($"{line.Key}={newValue}");
                    }
                    else
                    {
                        sb.AppendLine(line.Original);
                    }
                }
            }

            File.WriteAllText(_filePath, sb.ToString());
            _modifiedKeys.Clear();
        }

        public string? GetValue(string section, string key, string? defaultValue = null)
        {
            if (_data.TryGetValue(section, out var sectionData) &&
                sectionData.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue;
        }

        public void SetValue(string section, string key, string value)
        {
            if (!_data.ContainsKey(section))
            {
                _data[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            _data[section][key] = value;

            var line = _lines.FirstOrDefault(l => l.Section == section && l.Key != null &&
                l.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (line != null)
            {
                line.IsModified = true;
                _modifiedKeys.Add($"{section}.{key}");
            }
        }

        public bool GetBool(string section, string key, bool defaultValue = false)
        {
            var value = GetValue(section, key);
            if (bool.TryParse(value, out var result))
                return result;
            return defaultValue;
        }

        public void SetBool(string section, string key, bool value)
        {
            SetValue(section, key, value.ToString().ToLower());
        }

        public int GetInt(string section, string key, int defaultValue = 0)
        {
            var value = GetValue(section, key);
            if (int.TryParse(value, out var result))
                return result;
            return defaultValue;
        }

        public void SetInt(string section, string key, int value)
        {
            SetValue(section, key, value.ToString());
        }

        public bool HasChanges()
        {
            return _modifiedKeys.Count > 0;
        }

        public IReadOnlySet<string> GetModifiedKeys()
        {
            return _modifiedKeys;
        }

        public static IniFileHelper LoadOrCreate(string filePath)
        {
            return new IniFileHelper(filePath);
        }
    }
}
