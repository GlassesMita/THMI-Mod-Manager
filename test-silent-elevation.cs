using System;
using System.Diagnostics;
using THMI_Mod_Manager;

class TestSilentElevation
{
    static void Main()
    {
        Console.WriteLine("=== 测试静默权限提升 ===");
        
        // 测试当前完整性级别
        string currentLevel = SilentPermissionHelper.GetCurrentIntegrityLevel();
        Console.WriteLine($"当前完整性级别: {currentLevel}");
        
        // 测试特权状态
        string privilegeStatus = SilentPermissionHelper.GetPrivilegeStatus();
        Console.WriteLine($"特权状态:\n{privilegeStatus}");
        
        // 测试是否是管理员
        bool isAdmin = SilentPermissionHelper.IsRunAsAdmin();
        Console.WriteLine($"是否管理员: {isAdmin}");
        
        // 如果当前不是High完整性级别，尝试静默提升
        if (currentLevel != "High")
        {
            Console.WriteLine("\n尝试静默提升权限...");
            
            // 测试CMSTP方法
            Console.WriteLine("测试CMSTP白名单方法...");
            bool cmstpResult = SilentPermissionHelper.ElevateUsingWhiteList();
            Console.WriteLine($"CMSTP方法结果: {cmstpResult}");
            
            if (!cmstpResult)
            {
                // 测试计划任务方法
                Console.WriteLine("测试计划任务方法...");
                bool taskResult = SilentPermissionHelper.ElevateUsingScheduledTask();
                Console.WriteLine($"计划任务方法结果: {taskResult}");
            }
            
            // 重新检查完整性级别
            string newLevel = SilentPermissionHelper.GetCurrentIntegrityLevel();
            Console.WriteLine($"提升后完整性级别: {newLevel}");
            
            if (newLevel == "High")
            {
                Console.WriteLine("静默提升成功！配置特权...");
                bool configResult = SilentPermissionHelper.ConfigurePrivileges();
                Console.WriteLine($"特权配置结果: {configResult}");
                
                // 再次检查特权状态
                string newPrivilegeStatus = SilentPermissionHelper.GetPrivilegeStatus();
                Console.WriteLine($"配置后特权状态:\n{newPrivilegeStatus}");
            }
        }
        else
        {
            Console.WriteLine("\n已具有High完整性级别，配置特权...");
            bool configResult = SilentPermissionHelper.ConfigurePrivileges();
            Console.WriteLine($"特权配置结果: {configResult}");
            
            // 检查特权状态
            string newPrivilegeStatus = SilentPermissionHelper.GetPrivilegeStatus();
            Console.WriteLine($"配置后特权状态:\n{newPrivilegeStatus}");
        }
        
        Console.WriteLine("\n=== 测试完成 ===");
    }
}