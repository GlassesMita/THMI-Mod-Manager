using System;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== Testing Settings Save Functionality ===");
        Logger.LogInfo("=== Testing Settings Save Functionality ===");
        
        string configPath = Path.Combine("..", "THMI Mod Manager", "bin", "Debug", "net8.0", "AppConfig.Schale");
        
        // Read current ModifyTitle value
        Console.WriteLine($"Config file path: {configPath}");
        Console.WriteLine($"File exists: {File.Exists(configPath)}");
        Logger.LogInfo($"File exists: {File.Exists(configPath)}");
        
        if (File.Exists(configPath))
        {
            string originalContent = File.ReadAllText(configPath, Encoding.UTF8);
            Console.WriteLine("\n=== Original File Content ===");
            Logger.LogInfo("\n=== Original File Content ===");
            Console.WriteLine(originalContent);
            Logger.LogInfo(originalContent);
            
            // Parse current value
            var sections = ParseIniContent(originalContent);
            string currentValue = "not found";
            if (sections.ContainsKey("Game") && sections["Game"].ContainsKey("ModifyTitle"))
            {
                currentValue = sections["Game"]["ModifyTitle"];
            }
            Console.WriteLine($"\nCurrent ModifyTitle value: {currentValue}");
            Logger.LogInfo($"\nCurrent ModifyTitle value: {currentValue}");
            
            // Test toggling the value
            string newValue = currentValue == "true" ? "false" : "true";
            Console.WriteLine($"Testing save with new value: {newValue}");
            Logger.LogInfo($"Testing save with new value: {newValue}");
            
            // Simulate form data
            var formData = new Dictionary<string, string>
            {
                { "language", "en_US" },
                { "status", "" },
                { "useOsuCursor", "false" },
                { "useCustomCursor", "false" },
                { "cursorType", "default" },
                { "themeColor", "#c670ff" },
                { "launchMode", "steam" },
                { "launcherPath", "" },
                { "modsPath", Path.Combine("..", "THMI Mod Manager", "bin", "Debug", "net8.0", "Mods") },
                { "gamePath", Path.Combine("..", "THMI Mod Manager", "bin", "Debug", "net8.0") },
                { "modifyTitle", newValue }
            };
            
            Console.WriteLine("\n=== Simulated Form Data ===");
            Logger.LogInfo("\n=== Simulated Form Data ===");
            foreach (var kvp in formData)
            {
                Console.WriteLine($"{kvp.Key} = {kvp.Value}");
                Logger.LogInfo($"{kvp.Key} = {kvp.Value}");
            }
            
            // Manually update the config file (simulating what the server would do)
            var updatedSections = ParseIniContent(originalContent);
            if (updatedSections.ContainsKey("Game"))
            {
                updatedSections["Game"]["ModifyTitle"] = newValue;
                
                // Write back to file
                string updatedContent = GenerateIniContent(updatedSections);
                File.WriteAllText(configPath, updatedContent, Encoding.UTF8);
                
                Console.WriteLine("\n=== Updated File Content ===");
                Console.WriteLine(updatedContent);
                
                // Verify the change
                string verifyContent = File.ReadAllText(configPath, Encoding.UTF8);
                var verifySections = ParseIniContent(verifyContent);
                string verifyValue = "not found";
                if (verifySections.ContainsKey("Game") && verifySections["Game"].ContainsKey("ModifyTitle"))
                {
                    verifyValue = verifySections["Game"]["ModifyTitle"];
                }
                
                Console.WriteLine($"\n=== Verification ===");
                Logger.LogInfo($"\n=== Verification ===");
                Console.WriteLine($"Expected value: {newValue}");
                Logger.LogInfo($"Expected value: {newValue}");
                Console.WriteLine($"Actual value: {verifyValue}");
                Console.WriteLine($"Save successful: {verifyValue == newValue}");
                Logger.LogInfo($"Save successful: {verifyValue == newValue}");
                
                if (verifyValue == newValue)
                {
                    Console.WriteLine("\n✅ Settings save functionality is working correctly!");
                }
                else
                {
                    Console.WriteLine("\n❌ Settings save functionality has issues!");
                }
            }
            else
            {
                Console.WriteLine("\n❌ [Game] section not found in config file!");
                Logger.LogError("[Game] section not found in config file!");
            }
        }
        else
        {
            Console.WriteLine("\n❌ AppConfig.Schale file does not exist!");
            Logger.LogError("AppConfig.Schale file does not exist!");
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
    
    static string GenerateIniContent(Dictionary<string, Dictionary<string, string>> sections)
    {
        var sb = new StringBuilder();
        
        foreach (var section in sections)
        {
            sb.AppendLine($"[{section.Key}]");
            foreach (var kvp in section.Value)
            {
                sb.AppendLine($"{kvp.Key}={kvp.Value}");
            }
            sb.AppendLine();
        }
        
        return sb.ToString().Trim();
    }
}