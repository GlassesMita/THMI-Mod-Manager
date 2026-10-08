using System.IO;
using System.Runtime.InteropServices;

namespace THMI_Mod_Manager.Services;

/// <summary>
/// Windows 系统通知服务 —— 直接调用 Win32 Shell_NotifyIcon 气泡通知。
/// 完全绿色：不注册 AUMID / 快捷方式 / 服务 / 注册表，不写任何持久数据。
/// 仅在进程内临时添加托盘图标，气泡显示约 10 秒后自动删除，进程退出无任何残留。
/// 所有失败均静默降级，不影响主流程。
/// </summary>
public static class ToastService
{
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;

    private const uint NIIF_INFO = 0x00000001;

    private const uint WM_APP = 0x8000;

    private static readonly object Sync = new();
    private static IntPtr _hwnd;
    private static IntPtr _hIcon;
    private static bool _iconOwned;
    private static bool _added;
    private static int _pending;

    /// <summary>发送一条系统通知气泡；失败时静默降级。</summary>
    public static void Show(string title, string message)
    {
        // Wine/Proton（wine_get_version 存在）：托盘图标可用，但 NIF_INFO 气泡内容常不被
        // 兼容层显示，改走应用内 toast 保证通知可见；原生 Windows 保持托盘气泡路径。
        if (WineEnv.IsWine)
        {
            ShowInAppToast(title, message);
            return;
        }

        try
        {
            lock (Sync)
            {
                if (!_added && !EnsureShellIcon()) return;

                var nid = BuildData();
                nid.szInfoTitle = Truncate(title, 64);
                nid.szInfo = Truncate(message, 256);
                nid.dwInfoFlags = NIIF_INFO;
                nid.uFlags = NIF_INFO | NIF_ICON | NIF_TIP;
                Shell_NotifyIcon(NIM_MODIFY, ref nid);

                _pending++;
                _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ => RemoveTrayIcon());
            }
        }
        catch
        {
            // 通知失败不影响主流程
        }
    }

    /// <summary>首次调用时创建隐藏消息窗口并添加临时托盘图标。</summary>
    private static bool EnsureShellIcon()
    {
        try
        {
            if (_hwnd == IntPtr.Zero)
            {
                _hwnd = CreateWindowEx(0, "STATIC", "THMI_ToastWindow", 0, 0, 0, 0, 0,
                    new IntPtr(-3) /* HWND_MESSAGE */, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (_hwnd == IntPtr.Zero) return false;
            }

            _hIcon = ExtractAppIcon();
            var nid = BuildData();
            nid.uFlags = NIF_ICON | NIF_TIP | NIF_MESSAGE;
            nid.szTip = "THMI Mod Manager";
            nid.uCallbackMessage = WM_APP + 1;
            _added = Shell_NotifyIcon(NIM_ADD, ref nid);
            return _added;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>气泡显示结束后删除托盘图标（绿色：不留常驻系统痕迹）。</summary>
    private static void RemoveTrayIcon()
    {
        lock (Sync)
        {
            _pending--;
            if (_pending > 0 || !_added) return;

            var nid = BuildData();
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _added = false;

            if (_hIcon != IntPtr.Zero && _iconOwned)
                DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
            _iconOwned = false;
        }
    }

    /// <summary>从当前可执行文件提取图标（运行时，不落盘）；失败回退系统默认应用图标。</summary>
    private static IntPtr ExtractAppIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon is not null)
                {
                    _iconOwned = true; // 句柄所有权转交本服务，由 DestroyIcon 释放
                    return icon.Handle;
                }
            }
        }
        catch
        {
            // 回退系统图标
        }
        return LoadIcon(IntPtr.Zero, new IntPtr(32512) /* IDI_APPLICATION */);
    }

    private static NOTIFYICONDATA BuildData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = 1,
        hIcon = _hIcon,
        guidItem = Guid.Empty,
    };

    private static string Truncate(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxChars ? value : value[..maxChars];
    }

    /// <summary>
    /// Wine/Proton 下的应用内通知：右下角无边框提示窗，6 秒自动关闭。
    /// 与托盘路径一样保持"绿色"：不落盘、不留常驻窗口、进程退出无残留。
    /// </summary>
    private static void ShowInAppToast(string title, string message)
    {
        try
        {
            var app = System.Windows.Application.Current;
            var dispatcher = app?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            if (!dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(() => ShowInAppToast(title, message));
                return;
            }

            const double width = 320, height = 96;
            var workArea = System.Windows.SystemParameters.WorkArea;
            var toast = new System.Windows.Window
            {
                Title = "THMI Notification",
                Width = width,
                Height = height,
                WindowStyle = System.Windows.WindowStyle.None,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1F, 0x1F, 0x1F)),
                Left = Math.Max(0, workArea.Width - width - 16),
                Top = Math.Max(0, workArea.Height - height - 16),
                Content = new System.Windows.Controls.Border
                {
                    Padding = new System.Windows.Thickness(16),
                    Child = new System.Windows.Controls.StackPanel
                    {
                        Children =
                        {
                            new System.Windows.Controls.TextBlock
                            {
                                Text = title,
                                FontWeight = System.Windows.FontWeights.SemiBold,
                                Foreground = System.Windows.Media.Brushes.White,
                                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis
                            },
                            new System.Windows.Controls.TextBlock
                            {
                                Text = message,
                                Margin = new System.Windows.Thickness(0, 6, 0, 0),
                                TextWrapping = System.Windows.TextWrapping.Wrap,
                                MaxHeight = 44,
                                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD0, 0xD0, 0xD0))
                            }
                        }
                    }
                }
            };

            toast.Show();

            System.Windows.Threading.DispatcherTimer? timer = null;
            timer = new System.Windows.Threading.DispatcherTimer(
                TimeSpan.FromSeconds(6),
                System.Windows.Threading.DispatcherPriority.Background,
                (_, _) =>
                {
                    timer!.Stop();
                    try { toast.Close(); } catch { /* 已关闭则忽略 */ }
                },
                dispatcher);
            timer.Start();
        }
        catch
        {
            // 通知失败不影响主流程
        }
    }

    // ============ P/Invoke ============

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
        IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>NOTIFYICONDATA（Vista+ 布局，cbSize 由系统按版本兼容处理）。</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }
}
