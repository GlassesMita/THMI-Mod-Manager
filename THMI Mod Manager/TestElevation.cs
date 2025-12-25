using System;
using THMI_Mod_Manager;

namespace TestElevation
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== 测试权限提升 ===");
            
            // 测试是否是管理员
            bool isAdmin = PermissionHelper.IsRunAsAdmin();
            Console.WriteLine($"是否管理员: {isAdmin}");
            
            // 测试当前权限状态
            string permissionStatus = PermissionHelper.GetPermissionStatus();
            Console.WriteLine($"权限状态:\n{permissionStatus}");
            
            Console.WriteLine("\n=== 测试完成 ===");
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}