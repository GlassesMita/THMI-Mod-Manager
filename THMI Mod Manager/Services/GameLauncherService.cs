using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
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

    /// <summary>Steam 运行状态枚举：区分未运行 / 已运行（签名验证通过）/ 进程存在但签名异常。</summary>
    public enum SteamStatus
    {
        NotRunning,
        Running,
        SignatureMismatch,
    }

    /// <summary>
    /// 检测 Steam 客户端是否正在运行，并校验主进程的数字签名是否由 Valve Corp. 签发。
    /// Steam 主进程名为 steam.exe；其 Web 助手 steamwebhelper.exe 也常驻运行，
    /// 但仅凭 steamwebhelper.exe 无法区分客户端是否真正启动（可能残留后台进程），
    /// 因此优先以 steam.exe 为准。两者都无则判定 Steam 未运行。
    /// </summary>
    public static SteamStatus GetSteamStatus()
    {
        try
        {
            Process? steam = null;
            var found = false;
            foreach (var name in new[] { "steam", "steamwebhelper" })
            {
                var processes = Process.GetProcessesByName(name);
                if (processes.Length > 0)
                {
                    steam = processes[0];
                    found = true;
                    break;
                }
            }

            if (!found || steam is null)
                return SteamStatus.NotRunning;

            try
            {
                var isValveSigned = IsSignedByValve(steam.MainModule?.FileName);
                return isValveSigned ? SteamStatus.Running : SteamStatus.SignatureMismatch;
            }
            finally
            {
                steam.Dispose();
            }
        }
        catch
        {
            // 进程枚举失败（权限/系统异常）时保守判定为未运行，避免抛出打断 UI
            return SteamStatus.NotRunning;
        }
    }

    /// <summary>
    /// 校验指定可执行文件的 Authenticode 签名是否由 Valve Corp. 签发。
    /// 使用 X509Certificate2 读取签名者，比对 Subject 中的组织字段。
    /// </summary>
    private static bool IsSignedByValve(string? executablePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                return false;

            using var cert = X509Certificate2.CreateFromSignedFile(executablePath);
            if (cert is null)
                return false;

            var subject = cert.Subject;
            return subject.Contains("Valve", StringComparison.OrdinalIgnoreCase)
                || subject.Contains("Valve Corp.", StringComparison.OrdinalIgnoreCase)
                || subject.Contains("Valve Corporation", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // 读取签名失败（未签名/签名损坏/权限不足）时视为签名校验不通过
            return false;
        }
    }

    /// <summary>兼容旧调用点：是否检测到 Steam 进程（不校验签名）。</summary>
    public static bool IsSteamRunning() => GetSteamStatus() != SteamStatus.NotRunning;

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