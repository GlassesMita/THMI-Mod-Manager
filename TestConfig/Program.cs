using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== Testing AppConfig.Schale ===");
        
        string configPath = Path.Combine("..", "THMI Mod Manager", "AppConfig.Schale");
        
        Console.WriteLine($"Config file path: {configPath}");
        Console.WriteLine($"File exists: {File.Exists(configPath)}");
        Logger.LogInfo($"File exists: {File.Exists(configPath)}");
        
        if (File.Exists(configPath))
        {
            try
            {
                string content = File.ReadAllText(configPath, Encoding.UTF8);
                Console.WriteLine("\n=== File Content ===");
                Console.WriteLine(content);
                
                // Parse the INI-style content
                var sections = ParseIniContent(content);
                
                Console.WriteLine("\n=== Parsed Sections ===");
                Logger.LogInfo("\n=== Parsed Sections ===");
                foreach (var section in sections)
                {
                    Console.WriteLine($"[{section.Key}]");
                        Logger.LogInfo($"[{section.Key}]");
                    foreach (var kvp in section.Value)
                    {
                        Console.WriteLine($"  {kvp.Key} = {kvp.Value}");
                            Logger.LogInfo($"  {kvp.Key} = {kvp.Value}");
                    }
                }
                
                // Check for ModifyTitle in Game section
                if (sections.ContainsKey("Game"))
                {
                    var gameSection = sections["Game"];
                    if (gameSection.ContainsKey("ModifyTitle"))
                    {
                        string modifyTitleValue = gameSection["ModifyTitle"];
                        Console.WriteLine($"\n=== ModifyTitle Setting ===");
                        Console.WriteLine($"Value: {modifyTitleValue}");
                        Console.WriteLine($"Parsed as bool: {modifyTitleValue?.ToLower() != "false"}");
                    }
                    else
                    {
                        Console.WriteLine("\n=== ERROR ===");
                        Logger.LogError("\n=== ERROR ===");
                        Console.WriteLine("ModifyTitle setting not found in [Game] section!");
                        Logger.LogError("ModifyTitle setting not found in [Game] section!");
                    }
                }
                else
                {
                    Console.WriteLine("\n=== ERROR ===");
                    Console.WriteLine("[Game] section not found in config file!");
                    Logger.LogError("[Game] section not found in config file!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading config file: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("\n=== ERROR ===");
            Console.WriteLine("AppConfig.Schale file does not exist!");
        }
    }
    
    static Dictionary<string, Dictionary<string, string>> ParseIniContent(string content)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>();
        string currentSection = "";
        
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            string trimmedLine = line.Trim();
            
            // Skip comments and empty lines
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";"))
                continue;
            
            // Check for section header
            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                sections[currentSection] = new Dictionary<string, string>();
                continue;
            }
            
            // Parse key-value pairs
            var equalsIndex = trimmedLine.IndexOf('=');
            if (equalsIndex > 0 && !string.IsNullOrEmpty(currentSection))
            {
                string key = trimmedLine.Substring(0, equalsIndex).Trim();
                string value = trimmedLine.Substring(equalsIndex + 1).Trim();
                sections[currentSection][key] = value;
            }
        }
        
        return sections;
    }
}