using System;
using THMI_Mod_Manager.Tests;

namespace PerformanceTestRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== ModService Performance Test ===");
            Console.WriteLine();
            
            // Run the quick performance demo
            ModServicePerformanceTest.RunQuickTest();
            
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}