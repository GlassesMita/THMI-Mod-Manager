using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using THMI_Mod_Manager.Models;
using THMI_Mod_Manager.Services;
using Wpf.Ui.Appearance;
using FluentButton = Wpf.Ui.Controls.Button;
using FluentAppearance = Wpf.Ui.Controls.ControlAppearance;
using WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;

namespace THMI_Mod_Manager;

public partial class MainWindow : Window
{
    private readonly AppConfigManager _appConfig = new();
    private readonly LocalizationManager _localization = new();
    private readonly SessionTimeService _sessionTime = new();
    private readonly ModService _modService;
    private readonly ModUpdateService _modUpdateService;
    private readonly GameLauncherService _launcher;
    private readonly BepInExService _bepInExService;
    private StackPanel? _modsPanel;
    private string _modSortOrder = "name";
    private CancellationTokenSource? _modsRefreshCts;
    private bool _isCheckingUpdates;
    /// <summary>BepInEx 安装任务进行中标记，防止重复下载安装。</summary>
    private bool _isInstallingBepInEx;
    /// <summary>启动检测的 BepInEx 缺失提示每次会话最多弹一次，避免反复打扰。</summary>
    private bool _isBepInExPromptShown;
    /// <summary>BepInEx 引导弹窗打开中标记：ContentDialog 重复 ShowAsync 会抛异常，需防止并发。</summary>
    private bool _isBepInExDialogOpen;
    /// <summary>BepInEx 安装进度弹窗及其内部控件：检测/下载/安装全程展示，完成或出错前不可关闭。</summary>
    private Wpf.Ui.Controls.ContentDialog? _bepInExProgressDialog;
    private Wpf.Ui.Controls.ProgressRing? _bepInExProgressRing;
    /// <summary>原生 ProgressBar（WPF-UI 4.3.0 仅提供隐式样式，无独立控件类）。</summary>
    private System.Windows.Controls.ProgressBar? _bepInExProgressBar;
    private TextBlock? _bepInExProgressStatus;
    /// <summary>已打开的异常日志查看器（路径 → 窗口），避免同一日志重复打开多个窗口。</summary>
    private readonly Dictionary<string, EditorWindow> _exceptionLogViewers = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>周期刷新 Steam 运行状态，保持侧边栏指示灯与文本实时准确。</summary>
    private System.Windows.Threading.DispatcherTimer _steamStatusTimer = null!;

    public MainWindow()
    {
        InitializeComponent();
        _modService = new ModService(_appConfig);
        _modUpdateService = new ModUpdateService(_appConfig, new HttpClient());
        _launcher = new GameLauncherService(_appConfig, _sessionTime);
        _bepInExService = new BepInExService(_appConfig, new HttpClient());
        // 自管主题：不使用 SystemThemeWatcher —— 其首次 Watch 会把主题强制切到系统主题，
        // 并在系统广播主题消息时再次覆盖，导致固定 light/dark 模式在重启后被污染为深色。
        // 改为监听系统主题变化，仅在 system 模式下重算主题；固定模式完全不受系统干扰。
        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        ApplyTheme();
        ApplySidebarLocalization();
        new SystemInfoLogger(_appConfig, AppContext.BaseDirectory).LogApplicationStartup();
        ShowHome();
        InitializeSteamStatus();
        // 窗口加载完成后再探测 BepInEx：ContentDialog 依赖已加载的 DialogHost
        Loaded += (_, _) => _ = RunBepInExStartupCheckAsync();
    }

    /// <summary>初始化 Steam 状态检测：立即检查一次，随后每 5 秒定时刷新。</summary>
    private void InitializeSteamStatus()
    {
        _steamStatusTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        _steamStatusTimer.Tick += (_, _) => RefreshSteamStatus();
        _steamStatusTimer.Start();
        RefreshSteamStatus();
    }

    /// <summary>检测 Steam 是否运行并更新侧边栏指示灯（颜色）与状态文本。</summary>
    private void RefreshSteamStatus()
    {
        var status = GameLauncherService.GetSteamStatus();
        var (text, brush, dotBrush) = status switch
        {
            GameLauncherService.SteamStatus.Running
                => (_appConfig.GetLocalized("Buttons:Steam:Running", "Steam 运行中"), "SuccessBrush", "SuccessBrush"),
            GameLauncherService.SteamStatus.SignatureMismatch
                => (_appConfig.GetLocalized("Buttons:Steam:SignatureMismatch", "Steam 签名异常"), "WarningBrush", "WarningBrush"),
            _ => (_appConfig.GetLocalized("Buttons:Steam:NotRunning", "Steam 未运行"), "MutedTextBrush", "MutedTextBrush"),
        };
        SteamStatusText.Text = text;
        SteamStatusText.Foreground = (System.Windows.Media.Brush)FindResource(brush);
        SteamStatusDot.Fill = (System.Windows.Media.Brush)FindResource(dotBrush);
    }

    private void ShowHome_Click(object sender, RoutedEventArgs eventArgs) => ShowHome();
    private void ShowMods_Click(object sender, RoutedEventArgs eventArgs) => ShowMods();
    private void ShowSettings_Click(object sender, RoutedEventArgs eventArgs) => ShowSettings();

    /// <summary>
    /// 启动时后台探测 BepInEx IL2CPP 与 ModInjector；
    /// 缺失时弹窗引导下载最新版本（每次会话最多提示一次，可在设置中关闭自动检测）。
    /// </summary>
    private async Task RunBepInExStartupCheckAsync()
    {
        if (!GetConfigBool("[BepInEx]AutoCheck", true))
            return;

        try
        {
            var detection = await Task.Run(_bepInExService.Detect);
            if (detection.State == BepInExInstallState.Complete)
            {
                Logger.LogInfo($"BepInEx IL2CPP detected: {detection.InstalledVersion ?? "unknown version"} ({detection.Architecture})");
                return;
            }

            // await 后已回到 UI 线程，可直接弹窗
            await ShowBepInExInstallDialog(detection, onlyOnce: true);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "BepInEx startup check failed");
        }
    }

    /// <summary>
    /// BepInEx IL2CPP 缺失 / ModInjector 缺失时弹窗引导下载安装。
    /// onlyOnce 为 true 时（启动自动检测）每次会话最多提示一次；用户从卡片手动触发时为 false。
    /// </summary>
    private async Task ShowBepInExInstallDialog(BepInExDetectionResult detection, bool onlyOnce)
    {
        // 已完整安装时无引导意义（该弹窗仅服务于缺失/缺 ModInjector 两种状态）
        if (detection.State == BepInExInstallState.Complete)
            return;

        if (_isBepInExDialogOpen)
            return;

        if (onlyOnce)
        {
            if (_isBepInExPromptShown)
                return;
            _isBepInExPromptShown = true;
        }

        var (title, message) = detection.State == BepInExInstallState.InjectorMissing
            ? (_appConfig.GetLocalized("Settings:BepInExDialogInjectorTitle", "缺少 ModInjector"),
               _appConfig.GetLocalized("Settings:BepInExDialogInjectorMessage", "已检测到 BepInEx IL2CPP，但缺少 ModInjector（winhttp.dll 或 doorstop_config.ini），Plugin DLL 将无法被注入游戏。是否下载最新版本并补全？"))
            : (_appConfig.GetLocalized("Settings:BepInExDialogMissingTitle", "未检测到 BepInEx IL2CPP"),
               _appConfig.GetLocalized("Settings:BepInExDialogMissingMessage", "未检测到 BepInEx IL2CPP 运行环境，Plugin DLL 将无法被注入游戏，已安装的 Mod 也不会生效。是否自动下载并安装最新版本？"));

        var dialog = new Wpf.Ui.Controls.ContentDialog(RootDialogHost)
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 },
            PrimaryButtonText = _appConfig.GetLocalized("Settings:BepInExDialogDownload", "下载最新版本"),
            SecondaryButtonText = _appConfig.GetLocalized("Settings:BepInExDialogOpenPage", "打开下载页"),
            CloseButtonText = _appConfig.GetLocalized("Settings:BepInExDialogLater", "稍后再说"),
            DefaultButton = Wpf.Ui.Controls.ContentDialogButton.Primary,
        };

        _isBepInExDialogOpen = true;
        try
        {
            var result = await dialog.ShowAsync();
            if (result == Wpf.Ui.Controls.ContentDialogResult.Primary)
                await InstallBepInExAsync();
            else if (result == Wpf.Ui.Controls.ContentDialogResult.Secondary)
                OpenBepInExBuildsPage();
        }
        finally
        {
            _isBepInExDialogOpen = false;
        }
    }

    /// <summary>
    /// 获取最新版 BepInEx IL2CPP 构建并安装到游戏根目录。
    /// 检测/下载/安装全程展示带 Loading 图标与 ProgressBar 的进度弹窗，
    /// 弹窗无按钮且无 ESC/点击遮罩关闭途径，仅在安装成功或出错时关闭。
    /// </summary>
    private async Task InstallBepInExAsync()
    {
        if (_isInstallingBepInEx)
            return;

        _isInstallingBepInEx = true;
        ShowBepInExProgressDialog();
        try
        {
            var progress = new Progress<BepInExInstallProgress>(UpdateBepInExProgress);
            UpdateBepInExProgress(new BepInExInstallProgress(BepInExInstallPhase.Checking, 0, 0));

            var detection = await Task.Run(_bepInExService.Detect);
            var release = await _bepInExService.GetLatestReleaseAsync(detection.Architecture);
            if (release is null)
            {
                CloseBepInExProgressDialog();
                StatusText.Text = _appConfig.GetLocalized("Settings:BepInExInstallNoRelease", "未能获取 BepInEx IL2CPP 最新版本信息，请稍后重试或手动下载。");
                OpenBepInExBuildsPage();
                return;
            }

            // 已安装且构建号一致：跳过 34MB 的重复下载（安装版本含完整 commit 哈希，按 be.NNNN 构建号比较）
            var installedBuild = ParseBepInExBuildNumber(detection.InstalledVersion);
            if (detection.State == BepInExInstallState.Complete && installedBuild is not null && installedBuild == ParseBepInExBuildNumber(release.Version))
            {
                CloseBepInExProgressDialog();
                var upToDateMessage = string.Format(_appConfig.GetLocalized("Settings:BepInExUpToDate", "BepInEx IL2CPP 已是最新构建（{0}）。"), detection.InstalledVersion);
                StatusText.Text = upToDateMessage;
                ShowInWindowToast(_appConfig.GetLocalized("Settings:BepInExRuntimeTitle", "BepInEx 运行环境"), upToDateMessage);
                return;
            }

            var version = await Task.Run(() => _bepInExService.DownloadAndInstallAsync(release, progress));
            CloseBepInExProgressDialog();
            var successMessage = string.Format(_appConfig.GetLocalized("Settings:BepInExInstallSuccess", "BepInEx IL2CPP 安装成功：{0}"), version);
            StatusText.Text = successMessage;
            ShowInWindowToast(_appConfig.GetLocalized("Settings:BepInExRuntimeTitle", "BepInEx 运行环境"), successMessage);
        }
        catch (Exception ex)
        {
            CloseBepInExProgressDialog();
            Logger.LogException(ex, "BepInEx install failed");
            StatusText.Text = string.Format(_appConfig.GetLocalized("Settings:BepInExInstallFailed", "BepInEx 安装失败：{0}"), ex.Message);
            MessageBox.Show(this, string.Format(_appConfig.GetLocalized("Settings:BepInExInstallFailed", "BepInEx 安装失败：{0}"), ex.Message),
                _appConfig.GetLocalized("Settings:BepInExRuntimeTitle", "BepInEx 运行环境"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isInstallingBepInEx = false;
            RefreshCurrentPageForBepInEx();
        }
    }

    /// <summary>
    /// 打开 BepInEx 安装进度弹窗：Loading 图标 + ProgressBar。
    /// IsFooterVisible=false 隐藏全部按钮；WPF-UI ContentDialog 无 ESC/点击遮罩关闭途径，
    /// 因此弹窗只能由 CloseBepInExProgressDialog 的 Hide 调用关闭。
    /// </summary>
    private void ShowBepInExProgressDialog()
    {
        if (_bepInExProgressDialog is not null)
            return;

        _bepInExProgressRing = new Wpf.Ui.Controls.ProgressRing { Width = 30, Height = 30, IsIndeterminate = true };
        _bepInExProgressStatus = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0), TextWrapping = TextWrapping.Wrap };
        _bepInExProgressStatus.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        var header = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        header.Children.Add(_bepInExProgressRing);
        header.Children.Add(_bepInExProgressStatus);

        _bepInExProgressBar = new System.Windows.Controls.ProgressBar { Minimum = 0, Maximum = 100, IsIndeterminate = true, Margin = new Thickness(0, 20, 0, 0) };

        var body = new StackPanel { MinWidth = 360 };
        body.Children.Add(header);
        body.Children.Add(_bepInExProgressBar);

        var dialog = new Wpf.Ui.Controls.ContentDialog(RootDialogHost)
        {
            Title = _appConfig.GetLocalized("Settings:BepInExInstallProgressTitle", "正在安装 BepInEx IL2CPP"),
            Content = body,
            IsFooterVisible = false,
        };
        _bepInExProgressDialog = dialog;
        _ = dialog.ShowAsync();
    }

    /// <summary>按阶段更新进度弹窗：Checking/Extracting 为不定态，Downloading 显示百分比与字节数。</summary>
    private void UpdateBepInExProgress(BepInExInstallProgress progress)
    {
        if (_bepInExProgressDialog is null || _bepInExProgressBar is null || _bepInExProgressStatus is null)
            return;

        switch (progress.Phase)
        {
            case BepInExInstallPhase.Downloading when progress.TotalBytes > 0:
                _bepInExProgressBar.IsIndeterminate = false;
                _bepInExProgressBar.Value = progress.BytesDownloaded * 100d / progress.TotalBytes;
                _bepInExProgressStatus.Text = string.Format(
                    _appConfig.GetLocalized("Settings:BepInExInstallDownloadingPercent", "正在下载 {0}%（{1} / {2}）"),
                    Math.Floor(progress.BytesDownloaded * 100d / progress.TotalBytes),
                    FormatMegabytes(progress.BytesDownloaded),
                    FormatMegabytes(progress.TotalBytes));
                break;
            case BepInExInstallPhase.Downloading:
                _bepInExProgressBar.IsIndeterminate = true;
                _bepInExProgressStatus.Text = string.Format(
                    _appConfig.GetLocalized("Settings:BepInExInstallDownloadingSize", "已下载 {0}"),
                    FormatMegabytes(progress.BytesDownloaded));
                break;
            case BepInExInstallPhase.Extracting:
                _bepInExProgressBar.IsIndeterminate = true;
                _bepInExProgressStatus.Text = _appConfig.GetLocalized("Settings:BepInExInstallExtracting", "正在安装 BepInEx IL2CPP...");
                break;
            case BepInExInstallPhase.Checking:
                _bepInExProgressBar.IsIndeterminate = true;
                _bepInExProgressStatus.Text = _appConfig.GetLocalized("Settings:BepInExInstallChecking", "正在获取 BepInEx IL2CPP 最新版本...");
                break;
        }
    }

    /// <summary>关闭并释放进度弹窗（Hide 会让 ShowAsync 以 None 完成）。</summary>
    private void CloseBepInExProgressDialog()
    {
        _bepInExProgressDialog?.Hide(Wpf.Ui.Controls.ContentDialogResult.None);
        _bepInExProgressDialog = null;
        _bepInExProgressRing = null;
        _bepInExProgressBar = null;
        _bepInExProgressStatus = null;
    }

    /// <summary>窗体内 Toast：挂在窗口级 SnackbarPresenter 上，任意页面可见（区别于 Windows 系统级 Toast）。</summary>
    private void ShowInWindowToast(string title, string message, Wpf.Ui.Controls.ControlAppearance appearance = Wpf.Ui.Controls.ControlAppearance.Success)
    {
        new Wpf.Ui.Controls.Snackbar(SettingsSnackbarPresenter)
        {
            Title = title,
            Content = message,
            Appearance = appearance,
            Timeout = TimeSpan.FromSeconds(4)
        }.Show(true);
    }

    private static string FormatMegabytes(long bytes) => $"{bytes / 1024d / 1024d:0.0} MB";

    /// <summary>从 BepInEx 版本字符串（如 6.0.0-be.788+5b766a3）中提取构建号。</summary>
    private static string? ParseBepInExBuildNumber(string? version)
    {
        if (string.IsNullOrEmpty(version))
            return null;
        var match = Regex.Match(version, @"be\.(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>安装完成后刷新当前页面，使主页/模组页/设置页的 BepInEx 状态提示立即更新。</summary>
    private void RefreshCurrentPageForBepInEx()
    {
        if (NavHomeRadio.IsChecked == true) ShowHome();
        else if (NavModsRadio.IsChecked == true) ShowMods();
        else if (NavSettingsRadio.IsChecked == true) ShowSettings();
    }

    private void OpenBepInExBuildsPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(BepInExService.BuildsPageUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.LogException(ex, "Failed to open BepInEx builds page");
            StatusText.Text = ex.Message;
        }
    }

    /// <summary>BepInEx 未就绪时的警告卡片（主页 / 模组页复用），说明缺少 ModInjector 时 Plugin DLL 无法注入。</summary>
    private Border BuildBepInExWarningCard(BepInExDetectionResult detection)
    {
        var message = detection.State == BepInExInstallState.InjectorMissing
            ? _appConfig.GetLocalized("Settings:BepInExInjectorWarning", "已检测到 BepInEx IL2CPP，但缺少 ModInjector（winhttp.dll 或 doorstop_config.ini），Plugin DLL 无法被注入游戏。")
            : _appConfig.GetLocalized("Settings:BepInExMissingWarning", "未检测到 BepInEx IL2CPP 运行环境。缺少它时无法向游戏注入 Plugin DLL，已安装的 Mod 也不会生效。");

        var card = CreateCard();
        card.Margin = new Thickness(0, 16, 0, 0);
        card.BorderBrush = (System.Windows.Media.Brush)FindResource("WarningBrush");
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = message, Margin = new Thickness(16, 16, 16, 12), TextWrapping = TextWrapping.Wrap, Foreground = (System.Windows.Media.Brush)FindResource("TextBrush") });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 16, 16) };
        var downloadButton = CreateButton(_appConfig.GetLocalized("Settings:BepInExDialogDownload", "下载最新版本"), async (_, _) => await InstallBepInExAsync(), "PrimaryButton");
        downloadButton.Margin = new Thickness(0, 0, 8, 0);
        actions.Children.Add(downloadButton);
        actions.Children.Add(CreateButton(_appConfig.GetLocalized("Settings:BepInExDialogOpenPage", "打开下载页"), (_, _) => OpenBepInExBuildsPage()));
        body.Children.Add(actions);
        card.Child = body;
        return card;
    }

    /// <summary>
    /// 右键设置按钮：弹出 WinUI 3 风格输入框（ContentDialog）
    /// 输入异常类型（完整名称），确认后触发全局异常处理程序
    /// - UI 线程 → DispatcherUnhandledException → 弹控制台显示详情（主窗口冻结）→ 按键后关闭控制台恢复主窗口
    /// - 后台线程 → AppDomain.UnhandledException → 弹控制台显示详情 → 按键后进程退出
    /// </summary>
    private async void ShowSettingsContextMenu_Click(object sender, System.Windows.Input.MouseButtonEventArgs eventArgs)
    {
        eventArgs.Handled = true;

        var hintText = new TextBlock
        {
            Text = "输入异常类型（完整名称），确认后将触发全局异常处理程序：",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4),
        };
        hintText.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");

        var typeInput = new Wpf.Ui.Controls.TextBox
        {
            PlaceholderText = "例如：System.InvalidOperationException",
            Text = "System.InvalidOperationException",
            MinWidth = 340,
        };

        var backgroundToggle = new Wpf.Ui.Controls.ToggleSwitch
        {
            Content = "后台线程触发（弹出控制台后应用退出）",
            Margin = new Thickness(0, 12, 0, 0),
        };

        var contentPanel = new StackPanel
        {
            Children = { hintText, typeInput, backgroundToggle },
        };

        var dialog = new Wpf.Ui.Controls.ContentDialog(RootDialogHost)
        {
            Title = "触发异常测试",
            Content = contentPanel,
            PrimaryButtonText = "触发异常",
            CloseButtonText = "取消",
            DefaultButton = Wpf.Ui.Controls.ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        if (result != Wpf.Ui.Controls.ContentDialogResult.Primary)
            return;

        var exception = BuildException(typeInput.Text?.Trim());
        if (backgroundToggle.IsChecked == true)
            _ = Task.Run(() => throw exception);
        else
            throw exception;
    }

    /// <summary>
    /// 按用户输入的异常类型名称创建异常实例；无法解析时回退到 InvalidOperationException
    /// </summary>
    private static Exception BuildException(string? typeName)
    {
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            try
            {
                var type = ResolveExceptionType(typeName);
                if (type is not null && typeof(Exception).IsAssignableFrom(type))
                {
                    var ctor = type.GetConstructor(new[] { typeof(string) });
                    if (ctor is not null)
                        return (Exception)ctor.Invoke(new object[] { $"手动触发异常：{typeName}" });
                }
            }
            catch
            {
                // 类型无效时回退
            }
        }
        return new InvalidOperationException($"手动触发：UI 线程异常测试（输入类型无效：{typeName}）");
    }

    /// <summary>
    /// 按完整类型名解析异常类型。Type.GetType 在 .NET Core 下无法用简单名称
    /// 解析 System.Private.CoreLib 中的类型，失败时扫描所有已加载程序集。
    /// </summary>
    private static System.Type? ResolveExceptionType(string typeName)
    {
        var type = System.Type.GetType(typeName);
        if (type is not null)
            return type;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                type = asm.GetType(typeName);
            }
            catch
            {
                continue;
            }
            if (type is not null)
                return type;
        }
        return null;
    }

    private void ShowExplore_Click(object sender, RoutedEventArgs eventArgs) => ShowExplore();
    private void ShowLog_Click(object sender, RoutedEventArgs eventArgs) => ShowLog();
    private void ShowAbout_Click(object sender, RoutedEventArgs eventArgs) => ShowAbout();
    private void ShowShellAbout_Click(object sender, System.Windows.Input.MouseButtonEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        ShowShellAbout();
    }

    private void SidebarLaunchButton_Click(object sender, RoutedEventArgs eventArgs) => RunAction(_launcher.IsRunning ? _launcher.Stop : _launcher.Launch);

    private void ShowHome()
    {
        SetPageShell(_appConfig.GetLocalized("Index:Title", "主页"));
        ActivateNav(NavHomeRadio);
        var isGamePresent = File.Exists(Path.Combine(AppContext.BaseDirectory, "Touhou Mystia Izakaya.exe"));
        var panel = new StackPanel { MaxWidth = 940 };
        panel.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Index:WelcomeTitle", "Welcome"), FontSize = 30, FontWeight = FontWeights.SemiBold, Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush") });
        panel.Children.Add(new TextBlock { Text = "管理游戏、模组与启动配置。所有操作直接在本地完成。", Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 8, 0, 22) });

        var summary = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        summary.ColumnDefinitions.Add(new ColumnDefinition()); summary.ColumnDefinitions.Add(new ColumnDefinition()); summary.ColumnDefinitions.Add(new ColumnDefinition());
        summary.Children.Add(CreateMetricCard("游戏状态", _launcher.IsRunning ? "运行中" : "未运行", _launcher.IsRunning ? "#14866D" : "#72777D", 0));
        summary.Children.Add(CreateMetricCard("游戏文件", isGamePresent ? "已就绪" : "未找到", isGamePresent ? "#14866D" : "#AC6600", 1));
        summary.Children.Add(CreateMetricCard("本次计时", _sessionTime.GetFormattedTime(), "#C670FF", 2));
        panel.Children.Add(summary);

        var launchCard = CreateCard();
        var launchBody = new StackPanel { Margin = new Thickness(20) };
        launchBody.Children.Add(new TextBlock { Text = "游戏启动器", Style = (Style)FindResource("SectionTitle") });
        launchBody.Children.Add(new TextBlock { Text = _launcher.IsRunning ? "游戏正在运行。停止后才可以安全调整部分模组。" : "通过当前设置的启动方式打开游戏。", Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 6, 0, 16) });
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        controls.Children.Add(CreateButton(_launcher.IsRunning ? "停止游戏" : "启动游戏", (_, _) => RunAction(_launcher.IsRunning ? _launcher.Stop : _launcher.Launch), _launcher.IsRunning ? "DangerButton" : "PrimaryButton"));
        controls.Children.Add(CreateButton("打开模组管理", (_, _) => ShowMods()));
        launchBody.Children.Add(controls);
        launchCard.Child = launchBody;
        panel.Children.Add(launchCard);

        if (!isGamePresent)
        {
            var warning = CreateCard();
            warning.Margin = new Thickness(0, 16, 0, 0);
            warning.BorderBrush = (System.Windows.Media.Brush)FindResource("WarningBrush");
            warning.Child = new TextBlock { Text = "未在应用目录找到 Touhou Mystia Izakaya.exe。请将管理器部署到游戏目录，或在设置中配置外部启动程序。", Margin = new Thickness(16), Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"), TextWrapping = TextWrapping.Wrap };
            panel.Children.Add(warning);
        }

        var bepInExDetection = _bepInExService.Detect();
        if (bepInExDetection.State != BepInExInstallState.Complete)
            panel.Children.Add(BuildBepInExWarningCard(bepInExDetection));

        SetPageContent(panel);
        StatusText.Text = "就绪。";
    }

    private void ShowMods()
    {
        SetPageShell(_appConfig.GetLocalized("Mods:Title", "模组管理"));
        ActivateNav(NavModsRadio);
        var panel = new StackPanel { MaxWidth = 940 };
        var toolbarCard = CreateCard();
        var toolbar = new DockPanel { Margin = new Thickness(16) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(CreateButton("检查更新", async (_, _) => await CheckModUpdatesAsync()));
        actions.Children.Add(CreateButton("安装 ZIP", (_, _) => InstallMod(), "PrimaryButton"));
        actions.Children.Add(CreateButton("刷新", (_, _) => RefreshMods()));
        var sortOrder = new ComboBox { ItemsSource = new[] { "name", "date" }, SelectedItem = _modSortOrder, Width = 120, Margin = new Thickness(0, 0, 0, 0) };
        sortOrder.SelectionChanged += (_, _) => { _modSortOrder = sortOrder.SelectedItem?.ToString() ?? "name"; RefreshMods(); };
        actions.Children.Add(sortOrder);
        toolbar.Children.Add(actions);
        toolbar.Children.Add(new TextBlock { Text = "每个 Mod 均可直接启用、禁用或删除。", Style = (Style)FindResource("MutedText"), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right });
        toolbarCard.Child = toolbar;
        panel.Children.Add(toolbarCard);

        // BepInEx IL2CPP / ModInjector 缺失时，此页安装的 Plugin DLL 无法注入游戏，置顶提示
        var bepInExDetection = _bepInExService.Detect();
        if (bepInExDetection.State != BepInExInstallState.Complete)
            panel.Children.Add(BuildBepInExWarningCard(bepInExDetection));

        _modsPanel = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        panel.Children.Add(_modsPanel);
        SetPageContent(panel);
        RefreshMods();
    }

    private async void RefreshMods()
    {
        if (_modsPanel is null) return;
        _modsRefreshCts?.Cancel();
        var cts = new CancellationTokenSource();
        _modsRefreshCts = cts;
        var panel = _modsPanel;
        StatusText.Text = "正在加载 Mod...";
        try
        {
            var mods = await Task.Run(() => _modService.LoadMods(cts.Token), cts.Token);
            if (cts.IsCancellationRequested || panel != _modsPanel) return;
            mods = _modSortOrder == "date"
                ? mods.OrderByDescending(mod => mod.InstallTime).ToList()
                : mods.OrderBy(mod => mod.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            panel.Children.Clear();
            if (mods.Count > 0)
            {
                foreach (var mod in mods) panel.Children.Add(CreateModCard(mod));
            }
            StatusText.Text = mods.Count == 0 ? "BepInEx/plugins 目录及其子目录中没有找到 Mod。" : $"已加载 {mods.Count} 个 Mod。";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (panel == _modsPanel) StatusText.Text = $"加载 Mod 失败: {ex.Message}";
        }
    }

    private void ToggleSelectedMod()
    {
        throw new NotSupportedException("模组操作已改为卡片上的直接操作。");
    }

    private async void ToggleMod(ModInfo mod)
    {
        if (_launcher.IsRunning)
        {
            StatusText.Text = "游戏正在运行，不能修改 Mod 状态。";
            return;
        }

        var result = await Task.Run(() => _modService.ToggleMod(mod.FileName));
        if (!result.Success && result.ConflictingMods.Count > 0)
        {
            var content = new StackPanel { MaxWidth = 480 };
            content.Children.Add(new TextBlock { Text = result.ErrorMessage, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
            content.Children.Add(new TextBlock { Text = "冲突 Mod：", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
            foreach (var conflict in result.ConflictingMods)
                content.Children.Add(new TextBlock { Text = $"- {conflict.Name} {conflict.Version}", TextWrapping = TextWrapping.Wrap, Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush"), Margin = new Thickness(12, 0, 0, 2) });
            content.Children.Add(new TextBlock { Text = "是否禁用冲突项并强制启用？", Margin = new Thickness(0, 12, 0, 0) });

            var dialog = new Wpf.Ui.Controls.ContentDialog(RootDialogHost)
            {
                Title = "Mod 冲突",
                Content = content,
                PrimaryButtonText = "强制启用",
                CloseButtonText = "取消",
                DefaultButton = Wpf.Ui.Controls.ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() == Wpf.Ui.Controls.ContentDialogResult.Primary)
            {
                result = await Task.Run(() => _modService.ForceEnableMod(mod.FileName));
            }
        }

        StatusText.Text = result.Success ? "模组状态已更新。" : result.ErrorMessage ?? "无法更新模组状态。";
        RefreshMods();
    }

    private void InstallMod()
    {
        var dialog = new OpenFileDialog { Filter = "ZIP files (*.zip)|*.zip" };
        if (dialog.ShowDialog(this) != true) return;
        var installed = _modService.InstallMod(dialog.FileName);
        StatusText.Text = installed ? "Mod 安装成功。" : "Mod 安装失败。";
        RefreshMods();
    }

    private async Task CheckModUpdatesAsync()
    {
        if (_isCheckingUpdates)
        {
            StatusText.Text = "更新检查正在进行中，请稍候。";
            return;
        }

        _isCheckingUpdates = true;
        try
        {
            StatusText.Text = "正在检查模组更新...";
            var mods = await Task.Run(() => _modService.LoadMods());
            mods = await _modUpdateService.CheckForModUpdatesAsync(mods);
            var updatableCount = mods.Count(mod => mod.HasUpdateAvailable);
            StatusText.Text = $"检查完成，发现 {updatableCount} 个可更新模组。";
            if (updatableCount > 0 && GetConfigBool("[Notifications]Enable", false))
            {
                ToastService.Show(
                    _appConfig.GetLocalized("Notifications:UpdateFoundTitle", "发现模组更新"),
                    string.Format(_appConfig.GetLocalized("Notifications:UpdateFoundMessage", "发现 {0} 个可更新模组。"), updatableCount));
            }
            if (_modsPanel is not null)
            {
                _modsPanel.Children.Clear();
                foreach (var mod in mods) _modsPanel.Children.Add(CreateModCard(mod));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            StatusText.Text = $"检查更新失败: {exception.Message}";
        }
        finally
        {
            _isCheckingUpdates = false;
        }
    }

    private void ShowSettings()
    {
        SetPageShell(_appConfig.GetLocalized("Settings:Title", "设置"));
        ActivateNav(NavSettingsRadio);
        var panel = new StackPanel { MaxWidth = 960 };
        panel.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Settings:Header", "设置"), FontSize = 24, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Settings:Subtitle", "自定义您的使用体验"), Style = (Style)FindResource("MutedText"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 22) });

        // 语言与区域
        var language = new ComboBox { DisplayMemberPath = "FriendlyName", SelectedValuePath = "FileName", ItemsSource = _localization.GetAvailableLocales(), Margin = new Thickness(0, 4, 0, 14) };
        language.SelectedValue = _appConfig.Get("[Localization]Language", "en_US");
        panel.Children.Add(CreateSettingsCard(_appConfig.GetLocalized("Settings:SectionLanguage", "语言与区域"), _appConfig.GetLocalized("Settings:SectionLanguageDesc", "选择界面文本使用的语言。"), _appConfig.GetLocalized("Settings:SelectLanguageLabel", "界面语言"), language));

        // 外观（深浅主题 + 高对比度开关；主题色独立，不随主题切换）
        var themeMode = CreateCombo(new[] { ("system", _appConfig.GetLocalized("Settings:ThemeSystem", "跟随系统")), ("light", _appConfig.GetLocalized("Settings:ThemeLight", "浅色")), ("dark", _appConfig.GetLocalized("Settings:ThemeDark", "深色")) }, _appConfig.Get("[App]Theme", "system"));
        var highContrast = new Wpf.Ui.Controls.ToggleSwitch
        {
            OffContent = _appConfig.GetLocalized("Settings:HighContrastOff", "关闭"),
            OnContent = _appConfig.GetLocalized("Settings:HighContrastOn", "开启"),
            IsChecked = GetConfigBool("[App]HighContrast", false),
            Margin = new Thickness(0, 4, 0, 14)
        };
        panel.Children.Add(CreateSettingsCard(_appConfig.GetLocalized("Settings:SectionAppearance", "外观"), _appConfig.GetLocalized("Settings:SectionAppearanceDesc", "选择应用深浅主题，可开启高对比度。主题色保持独立，不随主题切换。"), _appConfig.GetLocalized("Settings:ThemeLabel", "主题"), themeMode, _appConfig.GetLocalized("Settings:HighContrastLabel", "高对比度"), highContrast));

        // 启动设置
        var launchMode = CreateCombo(new[] { ("steam_launch", _appConfig.GetLocalized("Settings:LaunchModeSteam", "Steam 启动")), ("external_program", _appConfig.GetLocalized("Settings:LaunchModeExternal", "外部程序")) }, _appConfig.Get("[Game]LaunchMode", "steam_launch"));
        var launchPath = new TextBox { Text = _appConfig.Get("[Game]LauncherPath", ""), Margin = new Thickness(0, 4, 0, 14) };
        var browseLauncher = CreateButton(_appConfig.GetLocalized("Common:Browse", "浏览"), (_, _) => BrowseFile(launchPath, "可执行文件 (*.exe)|*.exe"), "PrimaryButton");
        panel.Children.Add(CreateSettingsCard(_appConfig.GetLocalized("Settings:SectionLaunch", "启动设置"), _appConfig.GetLocalized("Settings:SectionLaunchDesc", "Steam 启动无需额外路径。选择外部程序时请指定可执行文件。"), _appConfig.GetLocalized("Settings:LaunchModeLabel", "启动方式"), launchMode, _appConfig.GetLocalized("Settings:LauncherPathLabel", "外部程序路径"), launchPath, browseLauncher));

        // 更新
        var autoCheckUpdates = new CheckBox { Content = _appConfig.GetLocalized("Updates:AutoCheckUpdates", "自动检查更新"), IsChecked = GetConfigBool("[Updates]CheckForUpdates", true), Margin = new Thickness(0, 0, 0, 14) };
        var updateFrequency = CreateCombo(new[] { ("startup", _appConfig.GetLocalized("Updates:FrequencyStartup", "启动时")), ("weekly", _appConfig.GetLocalized("Updates:FrequencyWeekly", "每周")), ("monthly", _appConfig.GetLocalized("Updates:FrequencyMonthly", "每月")) }, _appConfig.Get("[Updates]UpdateFrequency", "startup"));
        panel.Children.Add(CreateSettingsCard(_appConfig.GetLocalized("Settings:SectionUpdates", "更新"), _appConfig.GetLocalized("Settings:SectionUpdatesDesc", "设置更新检查策略。"), autoCheckUpdates, _appConfig.GetLocalized("Updates:UpdateFrequencyLabel", "检查频率"), updateFrequency));

        // 通知（Windows Toast 通知）
        var enableNotifications = new CheckBox { Content = _appConfig.GetLocalized("Notifications:EnableNotifications", "启用 Windows Toast 通知"), IsChecked = GetConfigBool("[Notifications]Enable", false), Margin = new Thickness(0, 0, 0, 8) };
        var testNotification = CreateButton(_appConfig.GetLocalized("Notifications:Test", "发送测试通知"), (_, _) => SendTestNotification());
        testNotification.Margin = new Thickness(8, 0, 0, 0);
        var notifyRow = new DockPanel();
        DockPanel.SetDock(testNotification, Dock.Right);
        notifyRow.Children.Add(testNotification);
        notifyRow.Children.Add(enableNotifications);
        var notifyBody = new StackPanel { Margin = new Thickness(20) };
        notifyBody.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Settings:SectionNotifications", "通知"), Style = (Style)FindResource("SectionTitle") });
        notifyBody.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Settings:SectionNotificationsDesc", "通过 Windows Toast 接收更新与事件通知。"), Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 5, 0, 14) });
        notifyBody.Children.Add(notifyRow);
        var notifyCard = CreateCard();
        notifyCard.Child = notifyBody;
        notifyCard.Margin = new Thickness(0, 0, 0, 16);
        panel.Children.Add(notifyCard);

        // 窗口标题
        var modifyTitle = new CheckBox { Content = _appConfig.GetLocalized("Settings:ModifyTitleDescription", "给游戏窗口标题添加 'Modded' 前缀"), IsChecked = GetConfigBool("[Game]ModifyTitle", true), Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(CreateSettingsCard(_appConfig.GetLocalized("Settings:SectionTitle", "窗口标题"), _appConfig.GetLocalized("Settings:SectionTitleDesc", "游戏运行时应用窗口标题设置。"), modifyTitle));

        // 配置文件编辑器
        var openEditor = CreateButton(_appConfig.GetLocalized("Settings:ConfigFileEditorOpen", "打开配置文件编辑器"), (_, _) => new EditorWindow().ShowDialog());
        panel.Children.Add(CreateSettingsCard(_appConfig.GetLocalized("Settings:SectionConfigFile", "配置文件"), _appConfig.GetLocalized("Settings:SectionConfigFileDesc", "使用内置编辑器查看和修改 AppConfig.Schale 等配置文件。"), _appConfig.GetLocalized("Settings:ConfigFileEditorLabel", "配置文件编辑器"), openEditor));

        // 异常日志
        panel.Children.Add(BuildExceptionLogsCard());

        // BepInEx 运行环境（自动探测 IL2CPP 与 ModInjector，缺失时引导下载）
        panel.Children.Add(BuildBepInExRuntimeCard());

        // BepInEx 配置
        panel.Children.Add(BuildBepInExSettingsCard());

        // 保存（保存后热重载）
        var savePanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0) };
        var saveButton = CreateButton(_appConfig.GetLocalized("Common:Save", "保存设置"), (_, _) =>
        {
            void ShowSnack(string title, string message, Wpf.Ui.Controls.ControlAppearance appearance)
            {
                new Wpf.Ui.Controls.Snackbar(SettingsSnackbarPresenter)
                {
                    Title = title,
                    Content = message,
                    Appearance = appearance,
                    Timeout = TimeSpan.FromSeconds(3)
                }.Show(true);
            }

            try
            {
                _appConfig.Set("[Localization]Language", language.SelectedValue?.ToString() ?? "en_US");
                _appConfig.Set("[App]Theme", themeMode.SelectedValue?.ToString() ?? "system");
                _appConfig.Set("[Game]LaunchMode", launchMode.SelectedValue?.ToString() ?? "steam_launch");
                _appConfig.Set("[Game]LauncherPath", launchPath.Text ?? string.Empty);
                _appConfig.Set("[Updates]CheckForUpdates", (autoCheckUpdates.IsChecked == true).ToString());
                _appConfig.Set("[Updates]UpdateFrequency", updateFrequency.SelectedValue?.ToString() ?? "startup");
                _appConfig.Set("[Notifications]Enable", (enableNotifications.IsChecked == true).ToString());
                _appConfig.Set("[Game]ModifyTitle", (modifyTitle.IsChecked == true).ToString().ToLowerInvariant());
                _appConfig.Set("[App]HighContrast", (highContrast.IsChecked == true).ToString());
                _appConfig.Reload();
                Logger.LogInfo("Configuration reloaded successfully");
                ApplyHotReloadSettings();
                ApplyTheme();
                ApplySidebarLocalization();
                ShowSettings();
                StatusText.Text = "设置已保存并立即生效。";
                ShowSnack("设置已保存", "设置已保存并立即生效。", Wpf.Ui.Controls.ControlAppearance.Success);
            }
            catch (Exception ex)
            {
                Logger.LogException("Failed to save settings", ex);
                StatusText.Text = $"保存设置失败: {ex.Message}";
                ShowSnack("保存失败", ex.Message, Wpf.Ui.Controls.ControlAppearance.Danger);
            }
        }, "PrimaryButton");
        saveButton.Margin = new Thickness(0);
        savePanel.Children.Add(saveButton);
        panel.Children.Add(savePanel);
        SetPageContent(panel);
        StatusText.Text = "配置直接写入 AppConfig.Schale。";
    }

    private void ShowLog()
    {
        SetPageShell("日志");
        var logPath = Logger.GetLogFilePath() ?? Path.Combine(AppContext.BaseDirectory, "Logs", "Latest.Log");
        var card = CreateCard();
        card.Child = new TextBox { Text = ReadLogTail(logPath), IsReadOnly = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(16), BorderThickness = new Thickness(0), FontFamily = (System.Windows.Media.FontFamily)FindResource("MonoFont"), FontSize = 12, MinHeight = 420 };
        SetPageContent(card);
        StatusText.Text = logPath;
    }

    /// <summary>只读取日志文件末尾（默认 256 KB），避免一次性载入整个日志造成内存峰值与 UI 卡顿。</summary>
    private static string ReadLogTail(string logPath, int maxBytes = 256 * 1024)
    {
        try
        {
            var info = new FileInfo(logPath);
            if (!info.Exists || info.Length == 0) return "尚无日志文件。";
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var start = Math.Max(0, info.Length - maxBytes);
            stream.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = reader.ReadToEnd();
            if (start > 0)
            {
                var newline = text.IndexOf('\n');
                if (newline >= 0) text = text[(newline + 1)..];
                text = "…（仅显示日志末尾，完整内容请查看文件）\n" + text;
            }
            return text;
        }
        catch (Exception exception)
        {
            return $"无法读取日志：{exception.Message}";
        }
    }

    private void ShowAbout()
    {
        SetPageShell("关于");
        ActivateNav(NavAboutRadio);
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        var card = CreateCard();
        card.MaxWidth = 720;
        var body = new StackPanel { Margin = new Thickness(28) };
        body.Children.Add(new TextBlock { Text = "THMI Mod Manager", FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush") });
        body.Children.Add(new TextBlock { Text = $"版本 {version}", Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 8, 0, 22) });
        body.Children.Add(new Separator());
        body.Children.Add(new TextBlock { Text = "为 Touhou Mystia Izakaya 提供本地 Mod 管理、启动和配置功能的原生 WPF 桌面客户端。", Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 22, 0, 22), TextWrapping = TextWrapping.Wrap });

        // 深色模式下 TextBlock 默认前景为黑色，标题与组件名必须显式引用主题文本画刷
        var componentsHeading = new TextBlock { Text = "开源组件", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) };
        componentsHeading.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        body.Children.Add(componentsHeading);
        foreach (var (name, license, source) in GetOpenSourceComponents())
        {
            var nameText = new TextBlock { Text = name, FontWeight = FontWeights.Medium, Margin = new Thickness(0, 4, 0, 0) };
            nameText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            body.Children.Add(nameText);
            body.Children.Add(CreateComponentSourceLine(license, source));
        }
        body.Children.Add(new TextBlock
        {
            Text = "本应用基于 GPL-3.0 协议开源。BepInEx IL2CPP 运行环境在需要时从 builds.bepinex.dev 下载，随包分发的 Segoe UI 字体副本遵循微软字体许可，再分发前请自行确认合规。",
            Style = (Style)FindResource("MutedText"),
            FontSize = 12,
            Margin = new Thickness(0, 14, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        card.Child = body;
        SetPageContent(card);
        StatusText.Text = "本应用基于 GPL-3.0 协议开源。";
    }

    /// <summary>组件来源行：URL 形式的来源渲染为可点击超链接（系统浏览器打开），纯文本保持原样。</summary>
    private FrameworkElement CreateComponentSourceLine(string license, string source)
    {
        var line = new TextBlock { Style = (Style)FindResource("MutedText"), FontSize = 12, Margin = new Thickness(0, 0, 0, 6) };
        line.Inlines.Add(new System.Windows.Documents.Run(license + "  ·  "));

        var isUrl = source.Contains('.', StringComparison.Ordinal) && !source.Contains(' ', StringComparison.Ordinal);
        if (!isUrl)
        {
            line.Inlines.Add(new System.Windows.Documents.Run(source));
            return line;
        }

        var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(source))
        {
            NavigateUri = new Uri($"https://{source}"),
            Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush"),
        };
        link.RequestNavigate += (_, e) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, $"Failed to open link: {e.Uri.AbsoluteUri}");
            }
        };
        line.Inlines.Add(link);
        return line;
    }

    /// <summary>应用依赖的开源组件清单（名称 / 许可证 / 来源）。</summary>
    private static System.Collections.Generic.IReadOnlyList<(string Name, string License, string Source)> GetOpenSourceComponents() => new[]
    {
        (".NET 10 / WPF", "MIT", "Microsoft"),
        ("WPF-UI 4.3.0", "MIT", "github.com/lepoco/wpfui"),
        ("AvalonEdit 6.3.1.120", "MIT", "github.com/icsharpcode/AvalonEdit"),
        ("System.Drawing.Common 10.0.5", "MIT", "github.com/dotnet/runtime"),
        ("Tommy（内嵌源码）", "MIT", "github.com/skwasjar/Tommy"),
        ("Fluent UI System Icons", "MIT", "github.com/microsoft/fluentui-system-icons"),
        ("Cascadia Mono", "SIL OFL 1.1", "github.com/microsoft/cascadia-code"),
        ("Segoe UI（内嵌字体）", "Microsoft 字体许可", "Microsoft"),
    };

    private void RunAction(Func<string> action)
    {
        try { StatusText.Text = action(); }
        catch (Exception exception) { StatusText.Text = exception.Message; Logger.LogException(exception, "Desktop operation failed"); }
        ShowHome();
    }

    private void ShowExplore()
    {
        SetPageShell(_appConfig.GetLocalized("Explore:Title", "Mod 浏览"));
        ActivateNav(NavExploreRadio);
        var panel = new StackPanel { MaxWidth = 1200 };
        panel.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Explore:Header", "Mod 浏览"), FontSize = 36, FontWeight = FontWeights.Bold, Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Explore:Subtitle", "发现并下载 Touhou Project Mod"), Style = (Style)FindResource("MutedText"), FontSize = 17, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 40) });
        var card = CreateCard();
        var content = new StackPanel { Margin = new Thickness(48), HorizontalAlignment = HorizontalAlignment.Stretch };
        content.Children.Add(new TextBlock { Text = "\uE721", FontFamily = (System.Windows.Media.FontFamily)FindResource("IconFont"), FontSize = 72, Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.65 });
        content.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Explore:PlaceholderTitle", "暂无可用站点"), FontSize = 28, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 8) });
        content.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Explore:PlaceholderDesc", "当前没有可用的 Mod 下载站点。此功能正在开发中，敬请期待！"), Style = (Style)FindResource("MutedText"), FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 28) });
        var features = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 28) };
        features.Children.Add(CreateExploreFeature("\uE721", _appConfig.GetLocalized("Explore:Feature1", "搜索和浏览 Mod")));
        features.Children.Add(CreateExploreFeature("\uE896", _appConfig.GetLocalized("Explore:Feature2", "一键下载安装")));
        features.Children.Add(CreateExploreFeature("\uE734", _appConfig.GetLocalized("Explore:Feature3", "查看 Mod 评分和评论")));
        features.Children.Add(CreateExploreFeature("\uE81C", _appConfig.GetLocalized("Explore:Feature4", "获取最新 Mod 更新")));
        content.Children.Add(features);
        content.Children.Add(new Border { Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E7F3FF")), BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush"), BorderThickness = new Thickness(4, 0, 0, 0), Padding = new Thickness(18), Child = new TextBlock { Text = _appConfig.GetLocalized("Explore:InfoText", "我们正在努力构建 Mod 生态系统，请关注后续更新。"), Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#004085")), TextWrapping = TextWrapping.Wrap } });
        card.Child = content;
        panel.Children.Add(card);
        SetPageContent(panel);
        StatusText.Text = "Mod 浏览功能正在开发中。";
    }

    private Border CreateExploreFeature(string icon, string text) => new()
    {
        Background = (System.Windows.Media.Brush)FindResource("CanvasBrush"),
        Margin = new Thickness(8), Padding = new Thickness(22), CornerRadius = new CornerRadius(12),
        Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = icon, FontFamily = (System.Windows.Media.FontFamily)FindResource("IconFont"), FontSize = 22, Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush"), Margin = new Thickness(0, 0, 16, 0) }, new TextBlock { Text = text, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center } } }
    };

    private void SetPageShell(string pageTitle) => Title = $"{pageTitle} - THMI Mod Manager";

    /// <summary>
    /// 同步侧边栏选中态：仅当用户未直接点击对应导航项时也保证高亮一致。
    /// </summary>
    private void SetPageContent(object content)
    {
        PageContent.BeginAnimation(UIElement.OpacityProperty, null);
        PageContent.Opacity = 0;
        PageContent.Content = content;

        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        PageContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
    }

    private static void ActivateNav(RadioButton button) => button.IsChecked = true;


    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ShellAbout(IntPtr windowHandle, string applicationName, string otherText, IntPtr iconHandle);

    [DllImport("shell32.dll")]
    private static extern IntPtr ExtractIcon(IntPtr instanceHandle, string executablePath, int iconIndex);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    private void ShowShellAbout()
    {
        // Wine/Proton（wine_get_version 存在）下 ShellAbout 弹出的是 Wine 风格系统关于框，
        // 改走应用内"关于"页，观感与多语言文案保持一致；原生 Windows 保留系统 ShellAbout。
        if (WineEnv.IsWine)
        {
            ShowAbout();
            return;
        }

        var applicationName = "THMI Mod Manager";
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        var iconHandle = string.IsNullOrWhiteSpace(executablePath) ? IntPtr.Zero : ExtractIcon(IntPtr.Zero, executablePath, 0);

        try
        {
            ShellAbout(new System.Windows.Interop.WindowInteropHelper(this).Handle, applicationName, $"{applicationName} {version}", iconHandle);
        }
        finally
        {
            if (iconHandle != IntPtr.Zero)
                DestroyIcon(iconHandle);
        }
    }

    private Border CreateModCard(ModInfo mod)
    {
        var card = CreateCard();
        var layout = new Grid { Margin = new Thickness(18) };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition()); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var details = new StackPanel();
        var heading = new StackPanel { Orientation = Orientation.Horizontal };
        heading.Children.Add(new TextBlock { Text = mod.Name, Style = (Style)FindResource("SectionTitle"), VerticalAlignment = VerticalAlignment.Center });
        heading.Children.Add(CreateBadge(mod.IsDisabled ? "已禁用" : "已启用", mod.IsDisabled ? "#72777D" : "#14866D"));
        if (mod.HasUpdateAvailable) heading.Children.Add(CreateBadge("可更新", "#AC6600"));
        if (mod.IsValid && !mod.HasManifest) heading.Children.Add(CreateBadge("无清单·基础模式", "#5A5A66"));
        details.Children.Add(heading);
        details.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(mod.Description) ? $"{mod.Author}  |  {mod.FileName}" : mod.Description, Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 6, 0, 0) });
        details.Children.Add(new TextBlock { Text = $"版本 {mod.Version}    作者 {mod.Author}", Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush"), FontSize = 12, Margin = new Thickness(0, 8, 0, 0) });
        if (!mod.IsValid)
        {
            details.Children.Add(new TextBlock { Text = $"警告: {mod.ErrorMessage}", Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush"), FontSize = 12, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap });
        }
        layout.Children.Add(details);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var detailsButton = CreateButton("详细信息", (_, _) => ToggleModDetails(mod, card));
        var toggleButton = CreateButton(mod.IsDisabled ? "启用" : "禁用", (_, _) => ToggleMod(mod), mod.IsDisabled ? "PrimaryButton" : null);
        var deleteButton = CreateButton("删除", (_, _) => { if (MessageBox.Show($"删除 {mod.Name}？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) { _modService.DeleteMod(mod.FileName); RefreshMods(); } }, "DangerButton");
        toggleButton.IsEnabled = !_launcher.IsRunning;
        deleteButton.IsEnabled = !_launcher.IsRunning;
        actions.Children.Add(detailsButton);
        actions.Children.Add(toggleButton);
        actions.Children.Add(deleteButton);
        Grid.SetColumn(actions, 1); layout.Children.Add(actions); card.Child = layout;
        return card;
    }

    private void ToggleModDetails(ModInfo mod, Border card)
    {
        if (card.Child is not Grid layout) return;
        if (layout.RowDefinitions.Count == 1)
        {
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var details = new StackPanel { Margin = new Thickness(18, 0, 18, 18) };
            details.Children.Add(new Separator { Margin = new Thickness(0, 12, 0, 12) });
            if (!string.IsNullOrWhiteSpace(mod.ModLink)) details.Children.Add(CreateDetailLine("链接", mod.ModLink));
            if (!string.IsNullOrWhiteSpace(mod.UniqueId)) details.Children.Add(CreateDetailLine("ID", mod.UniqueId));
            details.Children.Add(CreateDetailLine("文件", mod.FilePath));
            details.Children.Add(CreateDetailLine("安装时间", mod.InstallTime.ToString("yyyy-MM-dd HH:mm")));
            details.Children.Add(CreateDetailLine("最后修改", mod.LastModified.ToString("yyyy-MM-dd HH:mm")));
            details.Children.Add(CreateDetailLine("文件大小", $"{mod.FileSize / 1024d:N1} KB"));
            if (mod.IncompatibleWith.Count > 0) details.Children.Add(CreateDetailLine("不兼容", string.Join(", ", mod.IncompatibleWith)));
            Grid.SetRow(details, 1); Grid.SetColumnSpan(details, 2); layout.Children.Add(details);
        }
        else
        {
            layout.Children.RemoveAt(layout.Children.Count - 1);
            layout.RowDefinitions.RemoveAt(1);
        }
    }

    private TextBlock CreateDetailLine(string label, string value) => new() { Text = $"{label}: {value}", Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };

    private Border CreateSettingsCard(string title, string description, string firstLabel, Control firstControl, string? secondLabel = null, Control? secondControl = null)
    {
        var card = CreateCard();
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("SectionTitle") });
        body.Children.Add(new TextBlock { Text = description, Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 5, 0, 18) });
        body.Children.Add(CreateLabel(firstLabel)); body.Children.Add(firstControl);
        if (secondLabel is not null && secondControl is not null) { body.Children.Add(CreateLabel(secondLabel)); body.Children.Add(secondControl); }
        card.Child = body; card.Margin = new Thickness(0, 0, 0, 16); return card;
    }

    private Border CreateSettingsCard(string title, string description, Control firstControl, Control secondControl, string thirdLabel, Control thirdControl, string fourthLabel, Control fourthControl)
    {
        var card = CreateCard();
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("SectionTitle") });
        body.Children.Add(new TextBlock { Text = description, Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 5, 0, 18) });
        body.Children.Add(firstControl); body.Children.Add(secondControl);
        body.Children.Add(CreateLabel(thirdLabel)); body.Children.Add(thirdControl);
        body.Children.Add(CreateLabel(fourthLabel)); body.Children.Add(fourthControl);
        card.Child = body; card.Margin = new Thickness(0, 0, 0, 16); return card;
    }

    private Border CreateSettingsCard(string title, string description, string firstLabel, Control firstControl, string secondLabel, Control secondControl, string thirdLabel, Control thirdControl)
    {
        var card = CreateCard();
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("SectionTitle") });
        body.Children.Add(new TextBlock { Text = description, Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 5, 0, 18) });
        body.Children.Add(CreateLabel(firstLabel)); body.Children.Add(firstControl);
        body.Children.Add(CreateLabel(secondLabel)); body.Children.Add(secondControl);
        body.Children.Add(CreateLabel(thirdLabel)); body.Children.Add(thirdControl);
        card.Child = body; card.Margin = new Thickness(0, 0, 0, 16); return card;
    }

    private Border CreateSettingsCard(string title, string description, string firstLabel, Control firstControl, FluentButton browseButton)
    {
        var card = CreateCard();
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("SectionTitle") });
        body.Children.Add(new TextBlock { Text = description, Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 5, 0, 18) });
        body.Children.Add(CreateLabel(firstLabel));
        var row = new DockPanel();
        DockPanel.SetDock(browseButton, Dock.Right);
        browseButton.Margin = new Thickness(8, 0, 0, 0);
        row.Children.Add(browseButton); row.Children.Add(firstControl);
        body.Children.Add(row);
        card.Child = body; card.Margin = new Thickness(0, 0, 0, 16); return card;
    }

    private Border CreateSettingsCard(string title, string description, string firstLabel, Control firstControl, string secondLabel, Control secondControl, FluentButton browseButton)
    {
        var card = CreateSettingsCard(title, description, firstLabel, firstControl, secondLabel, secondControl);
        if (card.Child is StackPanel body)
        {
            body.Children.Remove(secondControl);
            var row = new DockPanel();
            DockPanel.SetDock(browseButton, Dock.Right);
            browseButton.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(browseButton); row.Children.Add(secondControl);
            body.Children.Add(row);
        }
        return card;
    }

    private Border CreateSettingsCard(string title, string description, CheckBox onlyControl)
    {
        var card = CreateCard();
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("SectionTitle") });
        body.Children.Add(new TextBlock { Text = description, Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 5, 0, 18) });
        body.Children.Add(onlyControl);
        card.Child = body; card.Margin = new Thickness(0, 0, 0, 16); return card;
    }

    private Border CreateSettingsCard(string title, string description, CheckBox firstControl, string secondLabel, Control secondControl)
    {
        var card = CreateCard();
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("SectionTitle") });
        body.Children.Add(new TextBlock { Text = description, Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 5, 0, 18) });
        body.Children.Add(firstControl);
        body.Children.Add(CreateLabel(secondLabel)); body.Children.Add(secondControl);
        card.Child = body; card.Margin = new Thickness(0, 0, 0, 16); return card;
    }

    /// <summary>发送一条 Windows Toast 测试通知（不依赖开关状态，用于验证功能）。</summary>
    private void SendTestNotification()
    {
        ToastService.Show(
            _appConfig.GetLocalized("Notifications:TestTitle", "THMI Mod Manager"),
            _appConfig.GetLocalized("Notifications:TestMessage", "这是一条测试通知，说明 Windows Toast 通知工作正常。"));
        StatusText.Text = _appConfig.GetLocalized("Notifications:TestSent", "已发送测试通知。");
    }

    private bool GetConfigBool(string key, bool defaultValue) => bool.TryParse(_appConfig.Get(key, defaultValue.ToString()), out var value) ? value : defaultValue;

    /// <summary>
    /// 异常日志管理卡片：列出 Logs\KernelPanic_*.log，支持只读打开、复制到剪贴板与删除。
    /// </summary>
    private Border BuildExceptionLogsCard()
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
        var card = CreateCard();
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Settings:SectionExceptionLogs", "异常日志"), Style = (Style)FindResource("SectionTitle") });
        body.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Settings:SectionExceptionLogsDesc", "查看应用运行期间记录的异常日志，可打开（只读）、复制到剪贴板或删除。"), Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 5, 0, 14) });

        var countText = new TextBlock { Style = (Style)FindResource("MutedText"), VerticalAlignment = VerticalAlignment.Center };
        var listPanel = new StackPanel();

        var refreshButton = CreateButton(_appConfig.GetLocalized("Settings:ExceptionLogsRefresh", "刷新"), (_, _) => RefreshExceptionLogList(logDir, listPanel, countText));
        var deleteAllButton = CreateButton(_appConfig.GetLocalized("Settings:ExceptionLogsDeleteAll", "全部清除"), (_, _) => DeleteAllExceptionLogs(logDir, listPanel, countText), "DangerButton");

        var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(deleteAllButton, Dock.Right);
        DockPanel.SetDock(refreshButton, Dock.Right);
        deleteAllButton.Margin = new Thickness(0);
        refreshButton.Margin = new Thickness(8, 0, 8, 0);
        toolbar.Children.Add(deleteAllButton);
        toolbar.Children.Add(refreshButton);
        toolbar.Children.Add(countText);
        body.Children.Add(toolbar);

        body.Children.Add(listPanel);
        RefreshExceptionLogList(logDir, listPanel, countText);

        card.Child = body;
        card.Margin = new Thickness(0, 0, 0, 16);
        return card;
    }

    /// <summary>刷新异常日志列表（重建子项并更新计数）。</summary>
    private void RefreshExceptionLogList(string logDir, StackPanel listPanel, TextBlock countText)
    {
        listPanel.Children.Clear();
        var files = Directory.Exists(logDir)
            ? Directory.GetFiles(logDir, "KernelPanic_*.log").OrderByDescending(p => p).ToList()
            : new List<string>();
        countText.Text = string.Format(_appConfig.GetLocalized("Settings:ExceptionLogsCount", "共 {0} 条异常日志"), files.Count);

        if (files.Count == 0)
        {
            listPanel.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Settings:ExceptionLogsEmpty", "暂无异常日志。"), Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 6, 0, 0) });
            return;
        }

        foreach (var file in files)
            listPanel.Children.Add(CreateExceptionLogRow(file, () => RefreshExceptionLogList(logDir, listPanel, countText)));
    }

    /// <summary>渲染单个异常日志行：时间 + 文件名 + 打开（只读）/ 复制 / 删除。</summary>
    private Grid CreateExceptionLogRow(string path, Action refresh)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = FormatLogTime(path), FontWeight = FontWeights.SemiBold });
        info.Children.Add(new TextBlock { Text = Path.GetFileName(path), Style = (Style)FindResource("MutedText"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });

        var openButton = CreateButton(_appConfig.GetLocalized("Settings:ExceptionLogsOpen", "打开"), (_, _) => OpenExceptionLogViewer(path), "PrimaryButton");
        var copyButton = CreateButton(_appConfig.GetLocalized("Settings:ExceptionLogsCopy", "复制"), (_, _) => CopyExceptionLog(path));
        var deleteButton = CreateButton(_appConfig.GetLocalized("Settings:ExceptionLogsDelete", "删除"), (_, _) => DeleteExceptionLog(path, refresh), "DangerButton");

        openButton.Margin = new Thickness(8, 0, 0, 0);
        copyButton.Margin = new Thickness(8, 0, 0, 0);
        deleteButton.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(info, 0);
        Grid.SetColumn(openButton, 1);
        Grid.SetColumn(copyButton, 2);
        Grid.SetColumn(deleteButton, 3);
        row.Children.Add(info);
        row.Children.Add(openButton);
        row.Children.Add(copyButton);
        row.Children.Add(deleteButton);
        return row;
    }

    /// <summary>
    /// 以非模态方式打开异常日志查看器，主窗口可继续交互。
    /// 同一日志只保留一个查看器窗口：已存在时激活并前置，而不是再开一个。
    /// </summary>
    private void OpenExceptionLogViewer(string path)
    {
        if (_exceptionLogViewers.TryGetValue(path, out var existing))
        {
            // 窗口可能已被用户关闭但尚未从字典移除（或正在关闭中）
            if (existing.IsLoaded && !existing.IsVisible)
                existing.Show();
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }

        var viewer = new EditorWindow(path, readOnly: true);
        _exceptionLogViewers[path] = viewer;
        viewer.Closed += (_, _) => _exceptionLogViewers.Remove(path);
        // 非模态且不设置 Owner：Owned 窗口在 Z 序上永远位于所有者之上，
        // 会导致查看器始终盖住主窗口、无法把主窗口带到前面交互。
        // 独立顶层窗口允许主窗口与查看器自由切换前后。
        viewer.Show();
    }

    /// <summary>从 KernelPanic_yyyyMMdd_HHmmss.log 文件名解析时间，失败则回退文件修改时间。</summary>
    private static string FormatLogTime(string path)
    {
        var parts = Path.GetFileNameWithoutExtension(path).Split('_');
        if (parts.Length >= 3 && parts[^2].Length == 8 && parts[^1].Length == 6
            && DateTime.TryParseExact(parts[^2] + parts[^1], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return time.ToString("yyyy-MM-dd HH:mm:ss");
        return File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>复制异常日志全文到剪贴板。</summary>
    private void CopyExceptionLog(string path)
    {
        try
        {
            Clipboard.SetText(File.ReadAllText(path));
            StatusText.Text = _appConfig.GetLocalized("Settings:ExceptionLogsCopied", "已复制到剪贴板。");
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, string.Format(_appConfig.GetLocalized("Settings:ExceptionLogsCopyFailed", "复制到剪贴板失败：{0}"), exception.Message),
                _appConfig.GetLocalized("Settings:ExceptionLogsErrorTitle", "异常日志"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>删除单个异常日志（带确认）。</summary>
    private void DeleteExceptionLog(string path, Action refresh)
    {
        if (MessageBox.Show(this, string.Format(_appConfig.GetLocalized("Settings:ExceptionLogsDeleteConfirm", "确定要删除 {0} 吗？"), Path.GetFileName(path)),
                _appConfig.GetLocalized("Settings:ExceptionLogsDelete", "删除"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            File.Delete(path);
            StatusText.Text = string.Format(_appConfig.GetLocalized("Settings:ExceptionLogsDeleted", "已删除：{0}"), Path.GetFileName(path));
            refresh();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, string.Format(_appConfig.GetLocalized("Settings:ExceptionLogsDeleteFailed", "删除失败：{0}"), exception.Message),
                _appConfig.GetLocalized("Settings:ExceptionLogsErrorTitle", "异常日志"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>删除全部异常日志（带确认）。</summary>
    private void DeleteAllExceptionLogs(string logDir, StackPanel listPanel, TextBlock countText)
    {
        var files = Directory.Exists(logDir) ? Directory.GetFiles(logDir, "KernelPanic_*.log") : Array.Empty<string>();
        if (files.Length == 0) return;
        if (MessageBox.Show(this, string.Format(_appConfig.GetLocalized("Settings:ExceptionLogsDeleteAllConfirm", "确定要删除全部 {0} 条异常日志吗？"), files.Length),
                _appConfig.GetLocalized("Settings:ExceptionLogsDeleteAll", "全部清除"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        foreach (var file in files)
        {
            try { File.Delete(file); }
            catch { /* 单个失败不阻断整体 */ }
        }
        StatusText.Text = _appConfig.GetLocalized("Settings:ExceptionLogsDeletedAll", "已清除全部异常日志。");
        RefreshExceptionLogList(logDir, listPanel, countText);
    }

    private void BrowseFile(TextBox target, string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        if (dialog.ShowDialog(this) == true)
            target.Text = dialog.FileName;
    }

    private Border CreateMetricCard(string label, string value, string accent, int column)
    {
        var card = CreateCard(); card.Margin = new Thickness(column == 0 ? 0 : 6, 0, column == 2 ? 0 : 6, 0);
        card.Child = new StackPanel { Margin = new Thickness(18), Children = { new TextBlock { Text = label, Style = (Style)FindResource("MutedText") }, new TextBlock { Text = value, FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(accent)), Margin = new Thickness(0, 6, 0, 0) } } };
        Grid.SetColumn(card, column); return card;
    }

    private Border CreateBadge(string text, string color) => new()
    {
        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)),
        Margin = new Thickness(10, 0, 0, 0),
        Padding = new Thickness(8, 3, 8, 3),
        VerticalAlignment = VerticalAlignment.Center,
        CornerRadius = new CornerRadius(10),
        Child = new TextBlock
        {
            Text = text,
            Style = (Style)FindResource("BadgeText"),
            Foreground = System.Windows.Media.Brushes.White
        }
    };
    private Border CreateCard() => new() { Style = (Style)FindResource("CardBorder") };
    private static TextBlock CreateLabel(string text)
    {
        var label = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) };
        // 动态引用 TextBrush：深色/高对比模式下自动适配，避免继承默认黑色
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        return label;
    }
    private FluentButton CreateButton(string content, RoutedEventHandler handler, string? style = null)
    {
        var button = new FluentButton
        {
            Content = content,
            Margin = new Thickness(0, 0, 8, 0),
            Appearance = style switch
            {
                "PrimaryButton" => FluentAppearance.Primary,
                "DangerButton" => FluentAppearance.Danger,
                _ => FluentAppearance.Secondary,
            },
        };
        button.Click += handler;
        return button;
    }

    private sealed record OptionItem(string Value, string Text);

    private ComboBox CreateCombo(IEnumerable<(string Value, string Text)> options, string? selected)
    {
        var combo = new ComboBox { DisplayMemberPath = "Text", SelectedValuePath = "Value", ItemsSource = options.Select(o => new OptionItem(o.Value, o.Text)).ToList(), Margin = new Thickness(0, 4, 0, 14) };
        combo.SelectedValue = selected;
        return combo;
    }

    private sealed record BepInExConfigItem(string Section, string Key, string Type, string? Default, IReadOnlyList<string>? Options, string LocKey)
    {
        public string? Value { get; set; }
    }

    private static List<(string Section, string Key, string Type, string Default, string[]? Options, string LocKey)> CreateBepInExDefinitions() => new()
    {
        ("Caching", "EnableAssemblyCache", "checkbox", "true", null, "Settings:BepInExEnableAssemblyCache"),
        ("Detours", "DetourProviderType", "select", "Default", new[] { "Default", "Dobby", "Funchook" }, "Settings:BepInExDetourProvider"),
        ("Harmony.Logger", "LogChannels", "text", "Warn, Error", null, "Settings:BepInExHarmonyLogChannels"),
        ("IL2CPP", "UpdateInteropAssemblies", "checkbox", "true", null, "Settings:BepInExUpdateInteropAssemblies"),
        ("IL2CPP", "UnityBaseLibrariesSource", "text", "https://unity.bepinex.dev/libraries/{VERSION}.zip", null, "Settings:BepInExUnityBaseLibrariesSource"),
        ("IL2CPP", "IL2CPPInteropAssembliesPath", "text", "{BepInEx}", null, "Settings:BepInExIL2CPPInteropAssembliesPath"),
        ("IL2CPP", "PreloadIL2CPPInteropAssemblies", "checkbox", "true", null, "Settings:BepInExPreloadIL2CPPInteropAssemblies"),
        ("Logging", "UnityLogListening", "checkbox", "true", null, "Settings:BepInExUnityLogListening"),
        ("Logging.Console", "Enabled", "checkbox", "true", null, "Settings:BepInExConsoleEnabled"),
        ("Logging.Console", "PreventClose", "checkbox", "false", null, "Settings:BepInExConsolePreventClose"),
        ("Logging.Console", "ShiftJisEncoding", "checkbox", "false", null, "Settings:BepInExConsoleShiftJisEncoding"),
        ("Logging.Console", "StandardOutType", "select", "Auto", new[] { "Auto", "ConsoleOut", "StandardOut" }, "Settings:BepInExConsoleStandardOutType"),
        ("Logging.Console", "LogLevels", "text", "Fatal, Error, Warning, Message, Info", null, "Settings:BepInExConsoleLogLevels"),
        ("Logging.Disk", "Enabled", "checkbox", "true", null, "Settings:BepInExDiskLogEnabled"),
        ("Logging.Disk", "AppendLog", "checkbox", "false", null, "Settings:BepInExDiskLogAppend"),
        ("Logging.Disk", "LogLevels", "text", "Fatal, Error, Warning, Message, Info", null, "Settings:BepInExDiskLogLevels"),
        ("Logging.Disk", "InstantFlushing", "checkbox", "false", null, "Settings:BepInExDiskLogInstantFlushing"),
        ("Logging.Disk", "ConcurrentFileLimit", "number", "5", null, "Settings:BepInExDiskLogConcurrentFileLimit"),
        ("Logging.Disk", "WriteUnityLog", "checkbox", "false", null, "Settings:BepInExWriteUnityLog"),
        ("Preloader", "HarmonyBackend", "select", "auto", new[] { "auto", "dynamicmethod", "methodbuilder", "cecil" }, "Settings:BepInExHarmonyBackend"),
        ("Preloader", "DumpAssemblies", "checkbox", "false", null, "Settings:BepInExDumpAssemblies"),
        ("Preloader", "LoadDumpedAssemblies", "checkbox", "false", null, "Settings:BepInExLoadDumpedAssemblies"),
        ("Preloader", "BreakBeforeLoadAssemblies", "checkbox", "false", null, "Settings:BepInExBreakBeforeLoadAssemblies"),
    };

    private Border BuildBepInExSettingsCard()
    {
        var card = CreateCard();
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Settings:SectionBepInEx", "BepInEx 配置"), Style = (Style)FindResource("SectionTitle") });
        body.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Settings:BepInExWarning", "此处的设置建议在指导下进行操作"), Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 5, 0, 18) });

        var bepInExPath = new TextBox { Text = _appConfig.Get("[BepInEx]ConfigPath", ""), Margin = new Thickness(0, 4, 0, 6) };
        var browseBepInEx = CreateButton(_appConfig.GetLocalized("Common:Browse", "浏览"), (_, _) => BrowseFile(bepInExPath, "BepInEx config (*.cfg)|*.cfg"), "PrimaryButton");
        body.Children.Add(CreateLabel(_appConfig.GetLocalized("Settings:BepInExConfigPath", "配置文件路径")));
        var pathRow = new DockPanel();
        DockPanel.SetDock(browseBepInEx, Dock.Right);
        browseBepInEx.Margin = new Thickness(8, 0, 0, 0);
        pathRow.Children.Add(browseBepInEx);
        pathRow.Children.Add(bepInExPath);
        body.Children.Add(pathRow);

        var itemsPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        var bepInExControls = new List<(BepInExConfigItem Item, Control Control)>();

        var configPath = DetectBepInExConfigPath();
        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
        {
            bepInExPath.Text = configPath;
            var ini = IniFileHelper.LoadOrCreate(configPath);
            foreach (var group in CreateBepInExDefinitions().GroupBy(d => d.Section))
            {
                itemsPanel.Children.Add(new TextBlock { Text = $"[{group.Key}]", FontWeight = FontWeights.SemiBold, Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush"), Margin = new Thickness(0, 12, 0, 4) });
                foreach (var def in group)
                {
                    var value = def.Type switch
                    {
                        "checkbox" => ini.GetBool(def.Section, def.Key, def.Default == "true").ToString().ToLower(),
                        "number" => ini.GetInt(def.Section, def.Key, int.TryParse(def.Default, out var d) ? d : 0).ToString(),
                        _ => ini.GetValue(def.Section, def.Key, def.Default) ?? ""
                    };
                    var item = new BepInExConfigItem(def.Section, def.Key, def.Type, def.Default, def.Options, def.LocKey) { Value = value };
                    var labelText = $"{def.Key} / {_appConfig.GetLocalized(def.LocKey, def.Key)}";
                    Control control;
                    if (item.Type == "checkbox")
                    {
                        control = new CheckBox { Content = labelText, IsChecked = value.ToLower() == "true", Margin = new Thickness(0, 6, 0, 4) };
                    }
                    else
                    {
                        var itemLabel = new TextBlock { Text = labelText, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 3) };
                        itemLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
                        itemsPanel.Children.Add(itemLabel);
                        control = item.Type == "select"
                            ? CreateCombo(item.Options?.Select(o => (o, o)) ?? Array.Empty<(string, string)>(), value)
                            : new TextBox { Text = value, Margin = new Thickness(0, 0, 0, 6) };
                    }
                    itemsPanel.Children.Add(control);
                    bepInExControls.Add((item, control));
                }
            }
        }
        else
        {
            itemsPanel.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Settings:BepInExConfigPathHelp", "选择 BepInEx.cfg 配置文件路径"), Style = (Style)FindResource("MutedText") });
        }
        body.Children.Add(itemsPanel);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(CreateButton(_appConfig.GetLocalized("Common:Save", "保存"), (_, _) => SaveBepInExSettings(bepInExPath, bepInExControls), "PrimaryButton"));
        buttons.Children.Add(CreateButton(_appConfig.GetLocalized("Settings:BepInExOpenEditor", "编辑配置文件"), (_, _) =>
        {
            // 直接编辑自动检测到的 BepInEx/config/BepInEx.cfg；保存后局部刷新设置页面重新读取
            var path = DetectBepInExConfigPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                StatusText.Text = _appConfig.GetLocalized("Settings:BepInExInvalidPath", "BepInEx 配置文件路径无效或文件不存在");
                return;
            }
            var editor = new EditorWindow(path);
            editor.ShowDialog();
            if (editor.Saved)
                ShowSettings();
        }));
        buttons.Children.Add(CreateButton(_appConfig.GetLocalized("Common:Reset", "恢复默认值"), (_, _) =>
        {
            foreach (var (item, control) in bepInExControls)
            {
                // 仅重置界面控件，不更新 item.Value，确保点击保存后能识别出差异并写回默认值
                switch (control)
                {
                    case CheckBox cb: cb.IsChecked = item.Default == "true"; break;
                    case ComboBox combo: combo.SelectedValue = item.Default; break;
                    case TextBox tb: tb.Text = item.Default ?? ""; break;
                }
            }
            StatusText.Text = _appConfig.GetLocalized("Settings:BepInExResetComplete", "已恢复默认值，请点击保存按钮来应用更改。");
        }));
        body.Children.Add(buttons);

        card.Child = body;
        card.Margin = new Thickness(0, 0, 0, 16);
        return card;
    }

    private void SaveBepInExSettings(TextBox bepInExPath, List<(BepInExConfigItem Item, Control Control)> bepInExControls)
    {
        var path = bepInExPath.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            StatusText.Text = _appConfig.GetLocalized("Settings:BepInExInvalidPath", "BepInEx 配置文件路径无效或文件不存在");
            return;
        }

        _appConfig.Set("[BepInEx]ConfigPath", path);
        var ini = IniFileHelper.LoadOrCreate(path);
        foreach (var (item, control) in bepInExControls)
        {
            var newValue = control switch
            {
                CheckBox cb => (cb.IsChecked == true).ToString().ToLower(),
                ComboBox combo => combo.SelectedValue?.ToString() ?? "",
                TextBox tb => tb.Text ?? "",
                _ => ""
            };
            if (!string.Equals(newValue, item.Value, StringComparison.OrdinalIgnoreCase))
            {
                if (item.Type == "checkbox") ini.SetBool(item.Section, item.Key, newValue == "true");
                else ini.SetValue(item.Section, item.Key, newValue);
            }
        }

        if (ini.HasChanges())
        {
            ini.Save();
            Logger.LogInfo($"BepInEx settings saved to {path}");
            StatusText.Text = "BepInEx 设置已保存，注释已保留!";
        }
        else
        {
            StatusText.Text = "BepInEx 设置无更改，配置文件保持不变!";
        }
    }

    /// <summary>
    /// BepInEx 运行环境卡片：展示探测状态与已装版本，提供手动检测安装、打开下载页与自动检测开关。
    /// </summary>
    private Border BuildBepInExRuntimeCard()
    {
        var card = CreateCard();
        var body = new StackPanel { Margin = new Thickness(20) };
        body.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Settings:BepInExRuntimeTitle", "BepInEx 运行环境"), Style = (Style)FindResource("SectionTitle") });
        body.Children.Add(new TextBlock { Text = _appConfig.GetLocalized("Settings:BepInExRuntimeDesc", "自动探测 BepInEx IL2CPP 与 ModInjector（winhttp.dll / doorstop_config.ini）。缺失时无法向游戏注入 Plugin DLL。"), Style = (Style)FindResource("MutedText"), Margin = new Thickness(0, 5, 0, 14), TextWrapping = TextWrapping.Wrap });

        var detection = _bepInExService.Detect();
        var statusText = detection.State switch
        {
            BepInExInstallState.Complete => string.Format(_appConfig.GetLocalized("Settings:BepInExRuntimeStatusComplete", "已安装 {0}（{1}）"), detection.InstalledVersion ?? "?", detection.Architecture),
            BepInExInstallState.InjectorMissing => string.Format(_appConfig.GetLocalized("Settings:BepInExRuntimeStatusInjectorMissing", "已安装 {0}，但缺少 ModInjector（winhttp.dll / doorstop_config.ini）"), detection.InstalledVersion ?? "?"),
            _ => _appConfig.GetLocalized("Settings:BepInExRuntimeStatusMissing", "未检测到 BepInEx IL2CPP"),
        };
        body.Children.Add(new TextBlock
        {
            Text = statusText,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)FindResource(detection.State == BepInExInstallState.Complete ? "SuccessBrush" : "WarningBrush"),
            Margin = new Thickness(0, 0, 0, 14),
            TextWrapping = TextWrapping.Wrap,
        });

        var autoCheck = new CheckBox
        {
            Content = _appConfig.GetLocalized("Settings:BepInExRuntimeAutoCheck", "启动时自动检测"),
            IsChecked = GetConfigBool("[BepInEx]AutoCheck", true),
            Margin = new Thickness(0, 0, 0, 12),
        };
        // Set 默认立即持久化，开关即时生效，无需等待“保存设置”
        autoCheck.Checked += (_, _) => _appConfig.Set("[BepInEx]AutoCheck", "true");
        autoCheck.Unchecked += (_, _) => _appConfig.Set("[BepInEx]AutoCheck", "false");
        body.Children.Add(autoCheck);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(CreateButton(_appConfig.GetLocalized("Settings:BepInExRuntimeCheckNow", "检测并安装最新版"), async (_, _) =>
        {
            var current = _bepInExService.Detect();
            if (current.State == BepInExInstallState.Complete)
                await InstallBepInExAsync();   // 已安装：直接检测并更新到最新构建
            else
                await ShowBepInExInstallDialog(current, onlyOnce: false);
        }, "PrimaryButton"));
        buttons.Children.Add(CreateButton(_appConfig.GetLocalized("Settings:BepInExDialogOpenPage", "打开下载页"), (_, _) => OpenBepInExBuildsPage()));
        body.Children.Add(buttons);

        card.Child = body;
        card.Margin = new Thickness(0, 0, 0, 16);
        return card;
    }

    private string? DetectBepInExConfigPath()
    {
        var saved = _appConfig.Get("[BepInEx]ConfigPath", "");
        if (!string.IsNullOrEmpty(saved) && File.Exists(saved)) return saved;

        var dir = AppContext.BaseDirectory;
        var candidates = new List<string> { Path.Combine(dir, "BepInEx", "config", "BepInEx.cfg") };
        for (var i = 0; i < 4; i++)
        {
            var parent = Directory.GetParent(dir)?.FullName;
            if (string.IsNullOrEmpty(parent)) break;
            candidates.Add(Path.Combine(parent, "BepInEx", "config", "BepInEx.cfg"));
            dir = parent;
        }

        return candidates.FirstOrDefault(File.Exists) ?? saved;
    }

    private void ApplyHotReloadSettings()
    {
        // 主题色
        if (TryParseColor(_appConfig.Get("[App]ThemeColor", "#c670ff"), out var accent))
        {
            Application.Current.Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(accent);
            Application.Current.Resources["AccentHoverBrush"] = new System.Windows.Media.SolidColorBrush(Darken(accent, 0.18));
            // 导航选中软背景：约 12% 透明度的主题色
            Application.Current.Resources["AccentSoftBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x1F, accent.R, accent.G, accent.B));
        }
        ApplyAccent();
    }

    private void ApplyTheme()
    {
        var theme = (_appConfig.Get("[App]Theme", "system") ?? "system").ToLowerInvariant();
        // system：跟随 Windows 当前主题（直接读注册表，不依赖 WPF-UI 缓存，保证实时）
        var isDark = theme switch
        {
            "dark" => true,
            "light" => false,
            _ => IsSystemDark()
        };
        var highContrast = GetConfigBool("[App]HighContrast", false);
        var appTheme = isDark ? ApplicationTheme.Dark : ApplicationTheme.Light;

        // None backdrop：保持不透明背景，避免 Mica/亚克力与外部 DWM 玻璃叠加导致全透明
        if (ApplicationThemeManager.GetAppTheme() != appTheme)
            ApplicationThemeManager.Apply(appTheme, WindowBackdropType.None, true);

        Logger.LogInfo($"Applying theme: config={theme}, isDark={isDark}, highContrast={highContrast}");
        ApplySemanticBrushes(isDark, highContrast);
        ApplyAccent();
    }

    /// <summary>将 Fluent accent 资源同步为项目主题色，使 Primary 按钮等 Fluent 控件保持品牌色。</summary>
    private void ApplyAccent()
    {
        if (TryParseColor(_appConfig.Get("[App]ThemeColor", "#c670ff"), out var accent))
            ApplicationAccentColorManager.Apply(accent, ApplicationThemeManager.GetAppTheme());
    }

    /// <summary>
    /// 系统主题（深浅）变化时触发：仅 system 模式重算主题；固定 light/dark 模式不受影响。
    /// 深浅切换时 Windows 广播 ImmersiveColorSet，SystemEvents 映射为 General/Color/VisualStyle 类别。
    /// </summary>
    private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle))
            return;

        Dispatcher.BeginInvoke(() =>
        {
            if ((_appConfig.Get("[App]Theme", "system") ?? "system").Equals("system", StringComparison.OrdinalIgnoreCase))
                ApplyTheme();
        });
    }

    /// <summary>读取 Windows 深浅主题设置（AppsUseLightTheme：0 = 深色）。
    /// Wine/Proton 前缀中没有 Personalize 键：GetValue 返回默认值 1 → 浅色主题，安全降级。</summary>
    private static bool IsSystemDark()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return value is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 自管语义画刷（不透明，保证任意主题下背景/文字对比度）。
    /// 不再从 Fluent 词典同步——4.x 中部分键不存在（ApplicationPageBackgroundThemeBrush）
    /// 或为半透明叠加色（LayerFillColorAltBrush），会导致深色模式背景永远偏亮。
    /// 高对比度模式在深浅主题下分别采用纯黑/纯白背景与纯白/纯黑文字。
    /// </summary>
    private void ApplySemanticBrushes(bool isDark, bool highContrast)
    {
        if (highContrast)
        {
            // 高对比度：适用于浅色与深色模式（参照 Windows 高对比度黑/白主题语义）
            Application.Current.Resources["CanvasBrush"] = new System.Windows.Media.SolidColorBrush(isDark ? System.Windows.Media.Color.FromRgb(0x00, 0x00, 0x00) : System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            Application.Current.Resources["SurfaceBrush"] = new System.Windows.Media.SolidColorBrush(isDark ? System.Windows.Media.Color.FromRgb(0x00, 0x00, 0x00) : System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            Application.Current.Resources["SidebarBrush"] = new System.Windows.Media.SolidColorBrush(isDark ? System.Windows.Media.Color.FromRgb(0x00, 0x00, 0x00) : System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            Application.Current.Resources["BorderBrush"] = new System.Windows.Media.SolidColorBrush(isDark ? System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF) : System.Windows.Media.Color.FromRgb(0x00, 0x00, 0x00));
            Application.Current.Resources["TextBrush"] = new System.Windows.Media.SolidColorBrush(isDark ? System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF) : System.Windows.Media.Color.FromRgb(0x00, 0x00, 0x00));
            Application.Current.Resources["MutedTextBrush"] = new System.Windows.Media.SolidColorBrush(isDark ? System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF) : System.Windows.Media.Color.FromRgb(0x00, 0x00, 0x00));
            Application.Current.Resources["SuccessBrush"] = new System.Windows.Media.SolidColorBrush(isDark ? System.Windows.Media.Color.FromRgb(0x4C, 0xFF, 0x9E) : System.Windows.Media.Color.FromRgb(0x00, 0x6B, 0x4F));
            Application.Current.Resources["DangerBrush"] = new System.Windows.Media.SolidColorBrush(isDark ? System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B) : System.Windows.Media.Color.FromRgb(0xB0, 0x00, 0x20));
            Application.Current.Resources["WarningBrush"] = new System.Windows.Media.SolidColorBrush(isDark ? System.Windows.Media.Color.FromRgb(0xFF, 0xD5, 0x4A) : System.Windows.Media.Color.FromRgb(0x7A, 0x4E, 0x00));
            Application.Current.Resources["NavHoverBrush"] = new System.Windows.Media.SolidColorBrush(isDark ? System.Windows.Media.Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF) : System.Windows.Media.Color.FromArgb(0x26, 0x00, 0x00, 0x00));
            return;
        }

        if (isDark)
        {
            Application.Current.Resources["CanvasBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x17, 0x18, 0x1A));
            Application.Current.Resources["SurfaceBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1F, 0x22));
            Application.Current.Resources["SidebarBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x12, 0x13, 0x15));
            Application.Current.Resources["BorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3A, 0x3C, 0x40));
            Application.Current.Resources["TextBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            Application.Current.Resources["MutedTextBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9B, 0xA0, 0xA6));
            Application.Current.Resources["SuccessBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xC3, 0x8A));
            Application.Current.Resources["DangerBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B));
            Application.Current.Resources["WarningBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0xA5, 0x0A));
            Application.Current.Resources["NavHoverBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
        }
        else
        {
            Application.Current.Resources["CanvasBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF8, 0xF9, 0xFA));
            Application.Current.Resources["SurfaceBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
            Application.Current.Resources["SidebarBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF2, 0xF3, 0xF5));
            Application.Current.Resources["BorderBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC8, 0xCC, 0xD1));
            Application.Current.Resources["TextBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x20, 0x21, 0x22));
            Application.Current.Resources["MutedTextBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x72, 0x77, 0x7D));
            Application.Current.Resources["SuccessBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x14, 0x86, 0x6D));
            Application.Current.Resources["DangerBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD7, 0x33, 0x33));
            Application.Current.Resources["WarningBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAC, 0x66, 0x00));
            Application.Current.Resources["NavHoverBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x12, 0x00, 0x00, 0x00));
        }
    }

    private void ApplySidebarLocalization()
    {
        NavGroupMainTitle.Text = _appConfig.GetLocalized("Sidebar:GroupMain", "主菜单");
        NavGroupManageTitle.Text = _appConfig.GetLocalized("Sidebar:GroupManage", "管理");
        NavHomeText.Text = _appConfig.GetLocalized("Sidebar:Home", "首页");
        NavModsText.Text = _appConfig.GetLocalized("Sidebar:Mods", "模组");
        NavExploreText.Text = _appConfig.GetLocalized("Sidebar:Explore", "探索");
        NavSettingsText.Text = _appConfig.GetLocalized("Sidebar:Settings", "设置");
        NavAboutText.Text = _appConfig.GetLocalized("Sidebar:About", "关于");
        SidebarLaunchButton.Content = _appConfig.GetLocalized("Buttons:Launch", "启动");
        // Steam 状态文本由 RefreshSteamStatus 维护（含运行/未运行两种状态），
        // 语言切换后重新刷新以应用新语言；指示灯颜色一并更新。
        RefreshSteamStatus();
    }

    private static bool TryParseColor(string? hex, out System.Windows.Media.Color color)
    {
        color = System.Windows.Media.Colors.Transparent;
        try
        {
            if (System.Windows.Media.ColorConverter.ConvertFromString(hex) is System.Windows.Media.Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static System.Windows.Media.Color Darken(System.Windows.Media.Color color, double factor)
        => System.Windows.Media.Color.FromRgb(
            (byte)Math.Round(color.R * (1 - factor)),
            (byte)Math.Round(color.G * (1 - factor)),
            (byte)Math.Round(color.B * (1 - factor)));
}