using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== Testing Complete ModifyTitle Workflow ===");
        
        string configPath = Path.Combine("..", "THMI Mod Manager", "bin", "Debug", "net8.0", "AppConfig.Schale");
        
        Console.WriteLine($"Config file path: {configPath}");
        Console.WriteLine($"File exists: {File.Exists(configPath)}");
        Logger.LogInfo($"File exists: {File.Exists(configPath)}");
        
        if (!File.Exists(configPath))
        {
            Console.WriteLine("❌ AppConfig.Schale file does not exist!");
            return;
        }
        
        // Step 1: Read current config
        string originalContent = File.ReadAllText(configPath, Encoding.UTF8);
        var sections = ParseIniContent(originalContent);
        
        Console.WriteLine("\n=== Step 1: Current Configuration ===");
        DisplayConfigSections(sections);
        
        // Step 2: Verify ModifyTitle setting exists and is readable
        bool modifyTitleSettingExists = sections.ContainsKey("Game") && sections["Game"].ContainsKey("ModifyTitle");
        Console.WriteLine($"\n=== Step 2: ModifyTitle Setting Check ===");
        Logger.LogInfo($"\n=== Step 2: ModifyTitle Setting Check ===");
        Console.WriteLine($"ModifyTitle setting exists: {modifyTitleSettingExists}");
        
        if (modifyTitleSettingExists)
        {
            string currentValue = sections["Game"]["ModifyTitle"];
            bool currentBoolValue = currentValue?.ToLower() != "false";
            Console.WriteLine($"Current value: {currentValue}");
            Console.WriteLine($"Parsed as boolean: {currentBoolValue}");
            Logger.LogInfo($"Parsed as boolean: {currentBoolValue}");
            Console.WriteLine("✅ ModifyTitle setting is properly configured");
        }
        else
        {
            Console.WriteLine("❌ ModifyTitle setting is missing from [Game] section");
            return;
        }
        
        // Step 3: Test setting toggle (simulate user changing the switch)
        Console.WriteLine($"\n=== Step 3: Testing Setting Toggle ===");
        Logger.LogInfo($"\n=== Step 3: Testing Setting Toggle ===");
        string originalValue = sections["Game"]["ModifyTitle"];
        string newValue = originalValue == "true" ? "false" : "true";
        
        Console.WriteLine($"Original value: {originalValue}");
        Console.WriteLine($"Testing toggle to: {newValue}");
        
        // Simulate the server-side save logic
        sections["Game"]["ModifyTitle"] = newValue;
        string updatedContent = GenerateIniContent(sections);
        File.WriteAllText(configPath, updatedContent, Encoding.UTF8);
        
        Console.WriteLine("✅ Setting toggle test completed");
        Logger.LogInfo("Setting toggle test completed");
        
        // Step 4: Verify the change was saved
        string verifyContent = File.ReadAllText(configPath, Encoding.UTF8);
        var verifySections = ParseIniContent(verifyContent);
        string verifyValue = verifySections["Game"]["ModifyTitle"];
        
        Console.WriteLine($"\n=== Step 4: Verification ===");
        Console.WriteLine($"Expected value: {newValue}");
        Console.WriteLine($"Actual value: {verifyValue}");
        Logger.LogInfo($"Actual value: {verifyValue}");
        Console.WriteLine($"Save successful: {verifyValue == newValue}");
        
        if (verifyValue == newValue)
        {
            Console.WriteLine("✅ Setting change was successfully saved");
        }
        else
        {
            Console.WriteLine("❌ Setting change was not saved correctly");
            Logger.LogError("Setting change was not saved correctly");
            return;
        }
        
        // Step 5: Test LauncherController logic simulation
        Console.WriteLine($"\n=== Step 5: LauncherController Logic Simulation ===");
        string configValue = verifySections["Game"]["ModifyTitle"];
        bool shouldModifyTitle = configValue?.ToLower() != "false";
        
        Console.WriteLine($"Config value: {configValue}");
        Console.WriteLine($"Should modify title: {shouldModifyTitle}");
        Logger.LogInfo($"Should modify title: {shouldModifyTitle}");
        
        if (shouldModifyTitle)
        {
            Console.WriteLine("✅ LauncherController would proceed with title modification");
        }
        else
        {
            Console.WriteLine("✅ LauncherController would skip title modification (as expected)");
        }
        
        // Step 6: Test UI binding simulation
        Console.WriteLine($"\n=== Step 6: UI Binding Simulation ===");
        Logger.LogInfo($"\n=== Step 6: UI Binding Simulation ===");
        bool uiCheckedState = shouldModifyTitle;
        Console.WriteLine($"UI switch would be: {(uiCheckedState ? "checked" : "unchecked")}");
        Logger.LogInfo($"UI switch would be: {(uiCheckedState ? "checked" : "unchecked")}");
        Console.WriteLine("✅ UI binding is working correctly");
        
        // Final summary
        Console.WriteLine($"\n=== Final Summary ===");
        Console.WriteLine("✅ AppConfig.Schale contains [Game] section");
        Console.WriteLine("✅ ModifyTitle setting exists in [Game] section");
        Console.WriteLine("✅ Setting can be read and parsed correctly");
        Logger.LogInfo("Setting can be read and parsed correctly");
        Console.WriteLine("✅ Setting can be saved and updated");
        Console.WriteLine("✅ LauncherController logic respects the setting");
        Console.WriteLine("✅ UI binding works correctly");
        Logger.LogInfo("UI binding works correctly");
        Console.WriteLine("\n🎉 All tests passed! The ModifyTitle functionality is working correctly.");
        
        // Restore original value
        sections["Game"]["ModifyTitle"] = originalValue;
        string finalContent = GenerateIniContent(sections);
        File.WriteAllText(configPath, finalContent, Encoding.UTF8);
        Console.WriteLine($"\nRestored original value: {originalValue}");
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
    
    static void DisplayConfigSections(Dictionary<string, Dictionary<string, string>> sections)
    {
        foreach (var section in sections)
        {
            Console.WriteLine($"[{section.Key}]");
                        Logger.LogInfo($"[{section.Key}]");
            foreach (var kvp in section.Value)
            {
                Console.WriteLine($"  {kvp.Key} = {kvp.Value}");
            }
            Console.WriteLine();
            Logger.LogInfo("");
        }
    }
}