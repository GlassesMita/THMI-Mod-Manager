using System;
using System.IO;

namespace TestAppConfig
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing AppConfig.Schale file...");
            
            string configPath = @"c:\Users\Mila\source\repos\THMI Mod Manager\THMI Mod Manager\AppConfig.Schale";
            
            if (File.Exists(configPath))
            {
                Console.WriteLine("AppConfig.Schale file exists.");
                
                string content = File.ReadAllText(configPath);
                Console.WriteLine("File content:");
                Console.WriteLine(content);
                
                // Check for ModifyTitle setting
                if (content.Contains("ModifyTitle"))
                {
                    Console.WriteLine("\n✓ ModifyTitle setting found in config file.");
                    
                    // Parse the value
                    var lines = content.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("ModifyTitle"))
                        {
                            Console.WriteLine($"Found line: {line.Trim()}");
                            
                            // Extract value
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                string value = parts[1].Trim();
                                Console.WriteLine($"ModifyTitle value: {value}");
                                
                                bool enabled = value.ToLower() != "false";
                                Console.WriteLine($"ModifyTitle enabled: {enabled}");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("\n✗ ModifyTitle setting NOT found in config file.");
                }
            }
            else
            {
                Console.WriteLine("AppConfig.Schale file does NOT exist.");
            }
            
            Console.WriteLine("\nTest completed.");
        }
    }
}