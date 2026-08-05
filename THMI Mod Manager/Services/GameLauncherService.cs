using System.Diagnostics;

namespace THMI_Mod_Manager.Services;

public sealed class GameLauncherService
{
    private const string SteamGameUrl = "steam://rungameid/1584090";
    private const string ProcessName = "Touhou Mystia Izakaya";
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
        return "已发送启动请求。";
    }

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