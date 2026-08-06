using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace THMI_Mod_Manager.Services;

public sealed class GameLauncherService
{
    private const string SteamGameUrl = "steam://rungameid/1584090";
    private const string ProcessName = "Touhou Mystia Izakaya";
    private const string UnityWindowClass = "UnityWndClass";
    private readonly AppConfigManager _appConfig;
    private readonly SessionTimeService _sessionTimeService;

    public GameLauncherService(AppConfigManager appConfig, SessionTimeService sessionTimeService)
    {
        _appConfig = appConfig;
        _sessionTimeService = sessionTimeService;
    }

    public bool IsRunning => Process.GetProcessesByName(ProcessName).Length > 0;

    public string Launch()
    {
        if (IsRunning)
            return "游戏已经在运行。";

        var launchMode = _appConfig.Get("[Game]LaunchMode", "steam_launch");
        var target = launchMode == "external_program"
            ? _appConfig.Get("[Game]LauncherPath", "")
            : SteamGameUrl;

        if (string.IsNullOrWhiteSpace(target))
            throw new InvalidOperationException("请先在设置中选择要启动的程序。");
        if (launchMode == "external_program" && !File.Exists(target))
            throw new FileNotFoundException("配置的启动程序不存在。", target);

        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        _sessionTimeService.StartSession();
        Logger.LogInfo($"Game launch requested using {launchMode}.");

        if (_appConfig.Get("[Game]ModifyTitle", "true").Equals("true", StringComparison.OrdinalIgnoreCase))
            _ = Task.Run(WaitForGameAndModifyTitle);

        return "已发送启动请求。";
    }

    /// <summary>
    /// 等待游戏进程与 Unity 主窗口出现，然后修改窗口标题。
    /// Steam 启动有延迟，因此分两阶段轮询：先等进程，再等窗口。
    /// </summary>
    private static void WaitForGameAndModifyTitle()
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        Process? game = null;
        try
        {
            while (DateTime.UtcNow < deadline)
            {
                var processes = Process.GetProcessesByName(ProcessName);
                if (processes.Length > 0)
                {
                    game = processes[0];
                    break;
                }
                Thread.Sleep(500);
            }

            if (game is null)
                return;

            deadline = DateTime.UtcNow.AddSeconds(90);
            while (DateTime.UtcNow < deadline)
            {
                var hwnd = FindUnityWindow(game.Id);
                if (hwnd != IntPtr.Zero)
                {
                    ModifyTitle(hwnd);
                    return;
                }
                Thread.Sleep(500);
            }
        }
        finally
        {
            game?.Dispose();
        }
    }

    /// <summary>
    /// 枚举顶层窗口，按进程 ID + Unity 窗口类名筛选出游戏主窗口。
    /// 仅按类名匹配可能命中多个窗口（BepInEx 控制台等），必须同时校验窗口
    /// 所属进程，才能准确找到目标窗口。
    /// </summary>
    private static IntPtr FindUnityWindow(int processId)
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;

            GetWindowThreadProcessId(hwnd, out var windowPid);
            if (windowPid != (uint)processId)
                return true;

            var className = new StringBuilder(256);
            GetClassName(hwnd, className, className.Capacity);
            if (className.ToString() == UnityWindowClass)
            {
                result = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static void ModifyTitle(IntPtr hwnd)
    {
        var title = new StringBuilder(256);
        GetWindowText(hwnd, title, title.Capacity);
        var original = title.ToString();
        if (original.StartsWith("Modded ", StringComparison.OrdinalIgnoreCase))
            return; // 已修改过，避免重复添加前缀

        SetWindowText(hwnd, $"Modded {original}");
        Logger.LogInfo($"Modified game window title to 'Modded {original}'.");
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(IntPtr hWnd, string text);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public string Stop()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        if (processes.Length == 0)
            return "游戏当前未运行。";

        foreach (var process in processes)
        {
            process.Kill();
            process.Dispose();
        }

        _sessionTimeService.StopSession();
        Logger.LogInfo($"Stopped {processes.Length} game process(es).");
        return $"已停止 {processes.Length} 个游戏进程。";
    }
}