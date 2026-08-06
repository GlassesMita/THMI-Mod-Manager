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
        // 弹出控制台窗口显示异常详情；等待按键期间 UI 线程阻塞，主窗口暂时冻结
        GlobalExceptionHandler.DisplayKernelPanicWithUI(eventArgs.Exception, waitForKey: true);
        Logger.LogException(eventArgs.Exception, "Unhandled WPF dispatcher exception");
        GlobalExceptionHandler.LogKernelPanic(eventArgs.Exception);
        MessageBox.Show(
            $"程序遇到未处理错误，详细信息已写入 Logs 目录。\n\n{eventArgs.Exception.Message}",
            "THMI Mod Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        // 查看完毕后关闭控制台，恢复主窗口
        GlobalExceptionHandler.CloseConsole();
        eventArgs.Handled = true;
    }
}