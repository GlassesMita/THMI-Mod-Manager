using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace THMI_Mod_Manager.Services
{
    public class TomlParser
    {
        public Dictionary<string, Dictionary<string, string>> Parse(string filePath)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var currentSection = "";
            var currentDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(filePath))
            {
                return result;
            }

            var lines = File.ReadAllLines(filePath, Encoding.UTF8);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#") || trimmedLine.StartsWith(";"))
                {
                    continue;
                }

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    if (!string.IsNullOrEmpty(currentSection) && currentDict.Count > 0)
                    {
                        result[currentSection] = new Dictionary<string, string>(currentDict, StringComparer.OrdinalIgnoreCase);
                    }
                    
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 1).Trim();
                    currentDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                var match = Regex.Match(trimmedLine, @"^([^=]+)=(.*)$");
                if (match.Success)
                {
                    var key = match.Groups[1].Value.Trim();
                    var value = match.Groups[2].Value.Trim();
                    
                    if (!string.IsNullOrEmpty(currentSection))
                    {
                        currentSection = "Global";
                    }
                    
                    currentDict[key] = value;
                }
            }

            if (!string.IsNullOrEmpty(currentSection) && currentDict.Count > 0)
            {
                result[currentSection] = new Dictionary<string, string>(currentDict, StringComparer.OrdinalIgnoreCase);
            }

            return result;
        }
    }
}
