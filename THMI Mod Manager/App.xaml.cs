using System.Windows;
using System.Windows.Threading;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        GlobalExceptionHandler.Initialize();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(eventArgs);
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