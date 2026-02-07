using System.Globalization;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.Server;
using THMI_Mod_Manager.Middleware;
using System.Diagnostics;
using THMI_Mod_Manager;
using THMI_Mod_Manager.Services;

// ===================== 初始化全局异常处理程序 =====================
GlobalExceptionHandler.Initialize();

// ===================== 关键修改1：将隐藏逻辑移到最顶部 + 增加安全校验 =====================
// Windows API 声明用于隐藏控制台窗口
[DllImport("kernel32.dll")]
static extern IntPtr GetConsoleWindow();

[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

const int SW_HIDE = 0;
const int SW_SHOW = 5;

// ===================== 检测--version参数（优先级最高） =====================
if (args != null && args.Contains("--version", StringComparer.OrdinalIgnoreCase))
{
    const string version = "0.10.0";
    
    Console.WriteLine($"THMI Mod Manager v{version}");
    Console.WriteLine();
    Console.WriteLine("Usage: THMI Mod Manager [options]");
    Console.WriteLine("用法: THMI Mod Manager [选项]");
    Console.WriteLine();
    Console.WriteLine("Options (参数优先级从高到低):");
    Console.WriteLine("选项 (参数优先级从高到低):");
    Console.WriteLine();
    Console.WriteLine("  --version              Display version information (Highest priority, standalone)");
    Console.WriteLine("  --version              显示版本信息 (最高优先级，单独使用)");
    Console.WriteLine();
    Console.WriteLine("  --no-console          Hide console window (High priority, can be combined with other params)");
    Console.WriteLine("  --no-console          隐藏控制台窗口 (高优先级，可与其他参数搭配)");
    Console.WriteLine();
    Console.WriteLine("  --no-newtab           Disable automatic browser opening (High priority, can only be combined with --no-console)");
    Console.WriteLine("  --no-newtab           禁用自动打开浏览器 (高优先级，只能与 --no-console 搭配)");
    Console.WriteLine();
    Console.WriteLine("  --open=<page>          Open specific page (Medium priority, can be combined with --no-console)");
    Console.WriteLine("  --open=<page>          打开指定页面 (中等优先级，可与 --no-console 搭配)");
    Console.WriteLine("  --open-debug-page      Open debug page (Medium priority, can be combined with --no-console)");
    Console.WriteLine("  --open-debug-page      打开调试页面 (中等优先级，可与 --no-console 搭配)");
    Console.WriteLine("  --updated-version=<ver>  Open What's New page for version (Medium priority, can be combined with --no-console)");
    Console.WriteLine("  --updated-version=<ver>  打开版本更新页面 (中等优先级，可与 --no-console 搭配)");
    Environment.Exit(0);
}
// ============================================================================

// 1. 优先检测--no-console参数（忽略大小写，增强健壮性）
bool hideConsole = args != null && args.Contains("--no-console", StringComparer.OrdinalIgnoreCase);

// 2. 仅在Windows系统执行隐藏逻辑（避免跨平台报错）
if (hideConsole && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    // 3. 循环尝试获取控制台句柄（解决启动初期句柄未创建的问题）
    int retryCount = 0;
    IntPtr consoleHandle = IntPtr.Zero;
    while (consoleHandle == IntPtr.Zero && retryCount < 5)
    {
        consoleHandle = GetConsoleWindow();
        if (consoleHandle == IntPtr.Zero)
        {
            System.Threading.Thread.Sleep(10); // 短暂等待句柄创建
            retryCount++;
        }
    }

    // 4. 确保获取到有效句柄后再隐藏
    if (consoleHandle != IntPtr.Zero)
    {
        ShowWindow(consoleHandle, SW_HIDE);
        // 额外调用一次确保隐藏生效（兜底处理）
        ShowWindow(consoleHandle, SW_HIDE);
    }
}
// ===================== 隐藏逻辑修改结束 =====================

// 辅助函数：控制台输出（同时输出到控制台和本地日志文件）
// 注意：--version 参数部分的直接 Console.WriteLine 调用不受此函数影响
void ConsoleOutput(string message)
{
    if (!hideConsole)
    {
        Console.WriteLine(message);
    }
    // 同时写入本地日志文件，确保控制台日志不会丢失
    try
    {
        // Logger.Log(message, Logger.LogLevel.Info);
    }
    catch
    {
        // 如果Logger不可用，静默失败
    }
}

#if DEBUG
Console.Title = "THMI Mod Manager - Console (Debug Build)";
#else
Console.Title = "THMI Mod Manager - Console";
#endif

// 简单的权限检查 - 仅使用管理员权限运行
ConsoleOutput("正在检查权限状态...");
Logger.LogInfo("Checking permission status...");

// 检查是否以管理员身份运行
bool isAdmin = PermissionHelper.IsRunAsAdmin();
ConsoleOutput($"管理员权限: {isAdmin}");
Logger.LogInfo($"Administrator privileges: {isAdmin}");

if (!isAdmin)
{
    ConsoleOutput("没有管理员权限，有可能会导致部分功能不可用，算了还是继续加载吧...");
    Logger.LogWarning("Administrator privileges not available. Some features may be limited. Proceeding with loading...");
    /* PermissionHelper.RestartAsAdministrator(); */
    /* Environment.Exit(0); */
}
else
{
    ConsoleOutput("已获得管理员权限，继续启动应用...");
    Logger.LogInfo("Administrator privileges acquired. Continuing application startup...");
}

string osArchitecture = RuntimeInformation.OSArchitecture.ToString();
string platform;

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    if (osArchitecture.Contains("Arm"))
    {
        platform = "Windows (ARM)";
        ConsoleOutput($"Warning: Windows ARM detected. Some features may be limited.");
        Logger.LogWarning($"Windows ARM detected. Some features may be limited.");
    }
    else
    {
        platform = "Windows";
    }
}
else
{
    platform = RuntimeInformation.OSDescription;
    ConsoleOutput($"Note: Running on non-Windows platform ({platform}). This is an early preview for future cross-platform support.");
    Logger.LogInfo($"Running on non-Windows platform ({platform}). This is an early preview for future cross-platform support.");
}

string runtimeId = RuntimeInformation.RuntimeIdentifier;
string arch = runtimeId.Contains("x64") ? "64-Bit" : (runtimeId.Contains("x86") ? "32-Bit" : "Unknown");
ConsoleOutput($"Platform: {platform}");
Logger.LogInfo($"Platform: {platform}");
ConsoleOutput($"Architecture: {arch}");
Logger.LogInfo($"Architecture: {arch}");

ConsoleOutput("\n");
Logger.Log("\t");

ConsoleOutput("Where All Miracles Begin / あまねく奇跡の始発点 / 所有奇迹的始发点");
Logger.LogInfo("Where All Miracles Begin / あまねく奇跡の始発点 / 所有奇迹的始发点");

Console.Write('\n');
Logger.Log("\t");

// 在应用启动前预加载本地化消息（避免在 ApplicationStarted 回调中读取文件）
string currentLanguageForMessages = "en_US";
string runningMessage = "Running on localhost:{port}";
string browserOpenedMessage = "Opening URL: {url}";
string? welcomeMessage = null;

// 尝试从 AppConfig 读取语言设置
try
{
    var tempConfigPath = Path.Combine(AppContext.BaseDirectory, "Config", "app.ini");
    if (File.Exists(tempConfigPath))
    {
        foreach (var line in File.ReadAllLines(tempConfigPath))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Language="))
            {
                currentLanguageForMessages = trimmed.Substring("Language=".Length).Replace("_", "-").Replace("-HANS", "").Replace("-HANT", "");
                if (currentLanguageForMessages.Length > 2 && currentLanguageForMessages.Contains("-"))
                {
                    try { currentLanguageForMessages = new System.Globalization.CultureInfo(currentLanguageForMessages).Name; } catch { }
                }
                break;
            }
        }
    }
}
catch { }

// 构建本地化文件路径并读取消息
var localizationFileForMessages = Path.Combine(AppContext.BaseDirectory, "Localization", $"{currentLanguageForMessages}.ini");
if (!File.Exists(localizationFileForMessages))
{
    localizationFileForMessages = Path.Combine(AppContext.BaseDirectory, "Localization", "en_US.ini");
}

if (File.Exists(localizationFileForMessages))
{
    try
    {
        var lines = File.ReadAllLines(localizationFileForMessages);
        string currentSection = "";
        
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
                continue;
            
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line.Substring(1, line.Length - 2).Trim();
            }
            else if (currentSection == "Console" || currentSection == "Messages")
            {
                var idx = line.IndexOf('=');
                if (idx > 0)
                {
                    var key = line.Substring(0, idx).Trim();
                    var value = line.Substring(idx + 1).Trim().Replace("\\n", "\n");
                    
                    if (key == "AppRunningMessage")
                        runningMessage = value;
                    else if (key == "BrowserOpenedMessage")
                        browserOpenedMessage = value;
                    else if (key == "WelcomeMessage")
                        welcomeMessage = value;
                }
            }
        }
    }
    catch { }
}

var builder = WebApplication.CreateBuilder(args);

string? updateVersion = null;
string? openPage = null;
bool openDebugPage = false;
// ===================== 关键修改1：新增--no-newtab参数检测（优先级最高） =====================
bool noNewTab = args != null && args.Contains("--no-newtab", StringComparer.OrdinalIgnoreCase);

// 输出日志提示--no-newtab参数已生效
if (noNewTab)
{
    ConsoleOutput("检测到--no-newtab参数，将禁用自动打开浏览器窗口");
    Logger.LogInfo("Detected --no-newtab parameter, automatic browser window opening is disabled");
}
// ==========================================================================================

foreach (var arg in args)
{
    if (arg.StartsWith("--updated-version="))
    {
        updateVersion = arg.Substring("--updated-version=".Length);
        ConsoleOutput($"Update version parameter detected: {updateVersion}");
        Logger.LogInfo($"Update version parameter detected: {updateVersion}");
        break;
    }
    else if (arg.StartsWith("--open="))
    {
        openPage = arg.Substring("--open=".Length);
        ConsoleOutput($"Open page parameter detected: {openPage}");
        Logger.LogInfo($"Open page parameter detected: {openPage}");
    }
    else if (arg == "--open-debug-page")
    {
        openDebugPage = true;
        ConsoleOutput($"Open debug page parameter detected");
        Logger.LogInfo($"Open debug page parameter detected");
    }
}

builder.Configuration["UpdateVersion"] = updateVersion ?? string.Empty;
builder.Configuration["OpenPage"] = openPage ?? string.Empty;

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
}); // 添加API控制器服务

// Add localization services and custom INI-based localizer factory
builder.Services.AddLocalization();
// 注册本地化服务
builder.Services.AddSingleton<IStringLocalizerFactory>(provider => 
{
    var env = provider.GetRequiredService<IWebHostEnvironment>();
    var serviceProvider = provider;
    return new THMI_Mod_Manager.Services.IniFileStringLocalizerFactory(env, serviceProvider);
});

// Register AppConfigManager
builder.Services.AddSingleton<THMI_Mod_Manager.Services.AppConfigManager>(provider => 
{
    var env = provider.GetRequiredService<IWebHostEnvironment>();
    var configuration = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<THMI_Mod_Manager.Services.AppConfigManager>>();
    var serviceProvider = provider;
    return new THMI_Mod_Manager.Services.AppConfigManager(env, configuration, logger, serviceProvider);
});
// Register LocalizationManager
builder.Services.AddSingleton<THMI_Mod_Manager.Services.LocalizationManager>();

// Register ModService
builder.Services.AddSingleton<THMI_Mod_Manager.Services.ModService>();

// Register ModUpdateService
builder.Services.AddHttpClient<THMI_Mod_Manager.Services.ModUpdateService>();
builder.Services.AddSingleton<THMI_Mod_Manager.Services.ModUpdateService>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<THMI_Mod_Manager.Services.ModUpdateService>>();
    var appConfig = provider.GetRequiredService<THMI_Mod_Manager.Services.AppConfigManager>();
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    return new THMI_Mod_Manager.Services.ModUpdateService(logger, appConfig, httpClientFactory.CreateClient("ModUpdate"));
});

// Register SystemInfoLogger
builder.Services.AddSingleton<THMI_Mod_Manager.Services.SystemInfoLogger>(provider => 
{
    var logger = provider.GetRequiredService<ILogger<THMI_Mod_Manager.Services.SystemInfoLogger>>();
    var env = provider.GetRequiredService<IWebHostEnvironment>();
    THMI_Mod_Manager.Services.AppConfigManager? appConfigManager = null;
    try
    {
        appConfigManager = provider.GetService<THMI_Mod_Manager.Services.AppConfigManager>();
    }
    catch
    {
        // AppConfigManager might not be available yet, will handle gracefully in the logger
    }
    return new THMI_Mod_Manager.Services.SystemInfoLogger(logger, appConfigManager, env.ContentRootPath);
});

// Register UpdateCheckService
builder.Services.AddHttpClient<THMI_Mod_Manager.Services.UpdateCheckService>();
builder.Services.AddSingleton<THMI_Mod_Manager.Services.UpdateCheckService>();

// Register UpdateModule
builder.Services.AddHttpClient<THMI_Mod_Manager.UpdateModule>();
builder.Services.AddSingleton<THMI_Mod_Manager.UpdateModule>();

// Register ModUpdate HttpClient
builder.Services.AddHttpClient("ModUpdate", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("THMI-Mod-Manager/1.0");
});

// Register SessionTimeService
builder.Services.AddSingleton<THMI_Mod_Manager.Services.SessionTimeService>();

// Register SessionTimeMonitor background service
builder.Services.AddHostedService<THMI_Mod_Manager.Services.SessionTimeMonitor>();

// Register WhatsNewController HttpClient
builder.Services.AddHttpClient<THMI_Mod_Manager.Controllers.WhatsNewController>();

// 检查端口是否可用
bool IsPortAvailable(int port)
{
    try
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
        listener.Start();
        listener.Stop();
        return true;
    }
    catch
    {
        return false;
    }
}

// 获取可用的端口号
int GetAvailablePort(int preferredPort, int minPort = 5000, int maxPort = 65535)
{
    // 首先尝试首选端口
    if (IsPortAvailable(preferredPort))
    {
        return preferredPort;
    }
    
    // 首选端口被占用，随机选择一个可用端口
    var random = new Random();
    int attempts = 0;
    int maxAttempts = 100;
    
    while (attempts < maxAttempts)
    {
        int randomPort = random.Next(minPort, maxPort + 1);
        if (IsPortAvailable(randomPort))
        {
            return randomPort;
        }
        attempts++;
    }
    
    // 如果随机尝试失败，尝试从minPort开始顺序查找
    for (int port = minPort; port <= maxPort; port++)
    {
        if (IsPortAvailable(port))
        {
            return port;
        }
    }
    
    // 如果所有端口都被占用，返回首选端口（让应用自己处理错误）
    return preferredPort;
}

// 设置应用URL
var configuration = builder.Configuration;
var configuredUrls = configuration["urls"] ?? configuration["Urls"];
int portToUse = 5000;

if (!string.IsNullOrEmpty(configuredUrls))
{
    // 如果配置了URL，尝试解析端口
    var url = configuredUrls.Split(';').FirstOrDefault() ?? "http://localhost:5000";
    var uri = new Uri(url);
    portToUse = uri.Port;
    
    // 如果端口是80或0，使用5000作为默认值
    if (portToUse == 80 || portToUse == 0)
    {
        portToUse = 5000;
    }
}

// 检查端口是否可用，如果不可用则随机选择一个
int availablePort = GetAvailablePort(portToUse);
builder.WebHost.UseUrls($"http://localhost:{availablePort}");

var app = builder.Build();

// Configure localization options by scanning the Localization folder for *.ini files
var localizationPath = Path.Combine(app.Environment.ContentRootPath, "Localization");
var supportedCultures = new List<CultureInfo>();
if (Directory.Exists(localizationPath))
{
    foreach (var file in Directory.GetFiles(localizationPath, "*.ini"))
    {
        var fileName = Path.GetFileNameWithoutExtension(file);
        var candidate = fileName.Replace('_', '-');
        try
        {
            var ci = new CultureInfo(candidate);
            supportedCultures.Add(new CultureInfo(ci.Name)); // normalized
        }
        catch
        {
            // ignore invalid culture file names
        }
    }
}

if (!supportedCultures.Any())
{
    // Default to English when no localization files are present
    supportedCultures.Add(new CultureInfo("en"));
}

// Remove duplicates (by Name)
supportedCultures = supportedCultures.GroupBy(c => c.Name).Select(g => g.First()).ToList();

var defaultCulture = supportedCultures[0];
var requestLocalizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(defaultCulture.Name),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// 使用动态本地化中间件，在请求本地化之前设置当前文化
app.UseDynamicLocalization();

// 应用请求本地化
app.UseRequestLocalization(requestLocalizationOptions);

// 使用设置向导中间件，检查是否为首次运行
app.UseSetupWizard();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers(); // 添加API控制器支持
app.MapRazorPages();

// Handle 404 - Page not found
app.MapFallbackToPage("/404");

// 在应用启动后输出运行信息
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        // Log system information on startup
        var systemInfoLogger = app.Services.GetRequiredService<THMI_Mod_Manager.Services.SystemInfoLogger>();
        systemInfoLogger.LogApplicationStartup();
        
        // 从服务器特性获取实际使用的端口
        var serverFeature = app.Services.GetRequiredService<IServer>();
        var addresses = serverFeature.Features.Get<IServerAddressesFeature>();
        var port = 5000;
        
        if (addresses != null && addresses.Addresses.Any())
        {
            var address = addresses.Addresses.FirstOrDefault();
            if (address != null)
            {
                var uri = new Uri(address);
                port = uri.Port;
            }
        }
        
        // 从本地化文件读取消息（使用预加载的消息，避免重复读取）
        string finalRunningMessage = runningMessage.Replace("{port}", port.ToString());
        string finalBrowserOpenedMessage = browserOpenedMessage.Replace("{url}", $"http://localhost:{port}");
        
        // 先输出欢迎消息（如果存在）
        if (!string.IsNullOrEmpty(welcomeMessage))
        {
            ConsoleOutput(welcomeMessage);
            Logger.LogInfo("Welcome message displayed");
        }
        
        ConsoleOutput(finalRunningMessage);
        Logger.LogInfo($"Application running on localhost:{port}");

        // ===================== 关键修改2：判断--no-newtab参数，优先级最高 =====================
        if (!noNewTab) // 仅当未传入--no-newtab时，才执行打开浏览器逻辑
        {
            List<string> urlsToOpen = new List<string>();

            if (openDebugPage)
            {
                urlsToOpen.Add($"http://localhost:{port}/DebugPage");
                ConsoleOutput($"Opening debug page");
                Logger.LogInfo($"Opening debug page");

                if (!string.IsNullOrEmpty(openPage) && openPage.ToLower() != "debugpage")
                {
                    urlsToOpen.Add($"http://localhost:{port}/{openPage}");
                    ConsoleOutput($"Opening specified page: {openPage}");
                    Logger.LogInfo($"Opening specified page: {openPage}");
                }
            }
            else if (!string.IsNullOrEmpty(openPage))
            {
                urlsToOpen.Add($"http://localhost:{port}/{openPage}");
                ConsoleOutput($"Opening specified page: {openPage}");
                Logger.LogInfo($"Opening specified page: {openPage}");
            }
            else if (!string.IsNullOrEmpty(updateVersion))
            {
                urlsToOpen.Add($"http://localhost:{port}/WhatsNew?version={updateVersion}");
                ConsoleOutput($"Opening What's New page for version: {updateVersion}");
                Logger.LogInfo($"Opening What's New page for version: {updateVersion}");
            }
            else
            {
                urlsToOpen.Add($"http://localhost:{port}");
            }

            foreach (var url in urlsToOpen)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                ConsoleOutput(finalBrowserOpenedMessage);
                Logger.LogInfo($"Browser opened with URL: {url}");
            }
        }
        // ======================================================================================
    }
    catch (Exception ex)
    {
        ConsoleOutput($"无法自动打开浏览器: {ex.Message}");
        Logger.LogError($"Cannot open browser automatically: {ex.Message}");
    }
});

// 应用停止时记录关机信息
lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        var systemInfoLogger = app.Services.GetRequiredService<THMI_Mod_Manager.Services.SystemInfoLogger>();
        systemInfoLogger.LogApplicationShutdown();
    }
    catch (Exception ex)
    {
        ConsoleOutput($"Error during shutdown logging: {ex.Message}");
        Logger.LogError($"Error during shutdown logging: {ex.Message}");
    }
});

app.Run();