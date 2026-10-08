using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        GlobalExceptionHandler.Initialize();
        ApplyWineCompatibility();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(eventArgs);
    }

    /// <summary>
    /// Wine/Proton（wine_get_version 存在）：系统中不存在 Segoe UI / Segoe MDL2 / Cascadia 字体，
    /// 切换到随程序集内嵌的字体资源（Assets/Fonts，Build Action=Resource，经 pack URI 加载）。
    /// 原生 Windows 不触碰内嵌字体，行为不变。
    /// </summary>
    private void ApplyWineCompatibility()
    {
        if (!WineEnv.IsWine) return;

        var fontFolder = new Uri("pack://application:,,,/Assets/Fonts/");
        Resources["AppFont"] = new FontFamily(fontFolder, "./#Segoe UI");
        Resources["IconFont"] = new FontFamily(fontFolder, "./#FluentSystemIcons-Regular"); // 开源图标字体（MIT，码位与 MDL2 兼容）
        Resources["MonoFont"] = new FontFamily(fontFolder, "./#Cascadia Mono");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        // 弹出控制台窗口显示异常详情；等待按键期间 UI 线程阻塞，主窗口暂时冻结。
        // 日志写入（KernelPanic_*.log 与 Latest.Log）由 DisplayKernelPanicWithUI 内部完成，
        // 控制台界面已包含完整异常信息，不再额外弹 Win32 MessageBox，避免重复弹窗与重复写日志。
        GlobalExceptionHandler.DisplayKernelPanicWithUI(eventArgs.Exception, waitForKey: true);
        // 查看完毕后关闭控制台，恢复主窗口
        GlobalExceptionHandler.CloseConsole();
        eventArgs.Handled = true;
    }
}