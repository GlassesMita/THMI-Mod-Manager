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
        Logger.LogException(eventArgs.Exception, "Unhandled WPF dispatcher exception");
        GlobalExceptionHandler.LogKernelPanic(eventArgs.Exception);
        MessageBox.Show(
            $"程序遇到未处理错误，详细信息已写入 Logs 目录。\n\n{eventArgs.Exception.Message}",
            "THMI Mod Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        eventArgs.Handled = true;
    }
}