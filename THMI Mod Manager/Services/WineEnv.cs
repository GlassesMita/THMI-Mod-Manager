using System;
using System.Runtime.InteropServices;

namespace THMI_Mod_Manager.Services;

/// <summary>
/// Wine/Proton 运行时探测。
/// 同一 win-x64 产物同时面向原生 Windows 与兼容层，编译期无法区分运行环境，
/// 因此所有 Wine 差异必须在运行时经 <see cref="IsWine"/> 判定后分支（不做预处理器双产物）。
/// 判据：Wine/Proton 的 ntdll.dll 独有导出符号 <c>wine_get_version</c>（Wine 官方推荐探测法）。
/// </summary>
public static class WineEnv
{
    private static readonly int _state; // 0 = 非 Windows，1 = Wine/Proton，2 = 原生 Windows

    static WineEnv()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                _state = 0;
                return;
            }

            _state = GetProcAddress(GetModuleHandle("ntdll.dll"), "wine_get_version") != IntPtr.Zero ? 1 : 2;
        }
        catch
        {
            _state = 2; // 探测失败按原生 Windows 处理，保持旧行为
        }
    }

    /// <summary>当前进程是否运行在 Wine/Proton 兼容层之上。</summary>
    public static bool IsWine => _state == 1;

    /// <summary>Wine 版本号（如 "10.2"，Proton 为其 Wine 基线）；原生 Windows 返回 null。</summary>
    public static string? WineVersion
    {
        get
        {
            if (_state != 1) return null;
            try { return Marshal.PtrToStringAnsi(wine_get_version()); }
            catch { return null; }
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    // 仅 Wine/Proton 的 ntdll 导出此符号；原生 Windows 上不会调用（IsWine gate），仅声明无害。
    [DllImport("ntdll.dll")]
    private static extern IntPtr wine_get_version();
}
