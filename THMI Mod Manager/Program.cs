using System.Globalization;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.Server;
using THMI_Mod_Manager.Middleware;
using System.Diagnostics;
using THMI_Mod_Manager;
using THMI_Mod_Manager.Services;

#if DEBUG
Console.Title = "THMI Mod Manager - Console (Debug Build)";
#else
Console.Title = "THMI Mod Manager - Console";
#endif

// 简单的权限检查 - 仅使用管理员权限运行
Console.WriteLine("正在检查权限状态...");
Logger.LogInfo("Checking permission status...");

// 检查是否以管理员身份运行
bool isAdmin = PermissionHelper.IsRunAsAdmin();
Console.WriteLine($"管理员权限: {isAdmin}");
Logger.LogInfo($"Administrator privileges: {isAdmin}");

if (!isAdmin)
{
    Console.WriteLine("没有管理员权限，有可能会导致部分功能不可用，算了还是继续加载吧...");
    Logger.LogWarning("Administrator privileges not available. Some features may be limited. Proceeding with loading...");
    /* PermissionHelper.RestartAsAdministrator(); */
    /* Environment.Exit(0); */
}
else
{
    Console.WriteLine("已获得管理员权限，继续启动应用...");
    Logger.LogInfo("Administrator privileges acquired. Continuing application startup...");
}

string osArchitecture = RuntimeInformation.OSArchitecture.ToString();
string platform;

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    if (osArchitecture.Contains("Arm"))
    {
        platform = "Windows (ARM)";
        Console.WriteLine($"Unsupported platform: {platform}. This application does not support Windows ARM.");
        Logger.LogError($"Unsupported platform: {platform}. This application does not support Windows ARM.");
        Environment.Exit(1);
    }
    else
    {
        platform = "Windows";
    }
}
else
{
    platform = RuntimeInformation.OSDescription;
    Console.WriteLine($"Unsupported platform: {platform}. This application only supports Windows.");
    Logger.LogError($"Unsupported platform: {platform}. This application only supports Windows.");
    Environment.Exit(1);
}

string runtimeId = RuntimeInformation.RuntimeIdentifier;
string arch = runtimeId.Contains("x64") ? "64-Bit" : (runtimeId.Contains("x86") ? "32-Bit" : "Unknown");
Console.WriteLine($"Platform: {platform}");
Logger.LogInfo($"Platform: {platform}");
Console.WriteLine($"Architecture: {arch}");
Logger.LogInfo($"Architecture: {arch}");

Console.Write("\n");
Logger.Log("\t");

Console.WriteLine("46 41 43 45 20 54 48 45 20 53 49 4E 2C 20");
Logger.LogInfo("46 41 43 45 20 54 48 45 20 53 49 4E 2C 20");

Console.WriteLine("53 41 56 45 20 54 48 45 20 45 2E 47 2E 4F");
Logger.LogInfo("53 41 56 45 20 54 48 45 20 45 2E 47 2E 4F");

Console.Write('\n');
Logger.Log("\t");



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers(); // 添加API控制器服务

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
        
        // 从本地化文件读取消息
        var appConfigManager = app.Services.GetRequiredService<THMI_Mod_Manager.Services.AppConfigManager>();
        string currentLanguage = appConfigManager.GetSection("Localization").TryGetValue("Language", out var langValue) ? langValue : "zh_CN";
        
        // 构建本地化文件路径
        var localizationFile = Path.Combine(app.Environment.ContentRootPath, "Localization", $"{currentLanguage}.ini");
        string runningMessage = $"Running on localhost:{port}"; // 默认消息
        string browserOpenedMessage = $"Opened URL: http://localhost:{port}"; // 默认消息
        string? welcomeMessage = null;
        
        if (File.Exists(localizationFile))
        {
            try
            {
                var lines = File.ReadAllLines(localizationFile, Encoding.UTF8);
                string currentSection = "";
                
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
                        continue;
                    
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }
                    
                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    
                    var key = line.Substring(0, idx).Trim();
                    var value = line.Substring(idx + 1).Trim();
                    
                    // 解析换行符（支持\n作为换行符）
                    value = value.Replace("\\n", "\n");
                    
                    if (currentSection == "Console" || currentSection == "Messages")
                    {
                        if (key == "AppRunningMessage")
                            runningMessage = value.Replace("{port}", port.ToString());
                        else if (key == "BrowserOpenedMessage")
                            browserOpenedMessage = value.Replace("{url}", $"http://localhost:{port}");
                        else if (key == "WelcomeMessage")
                            welcomeMessage = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取本地化文件失败: {ex.Message}");
                Logger.LogError($"Failed to read localization file: {ex.Message}");
            }
        }
        
        // 先输出欢迎消息（如果存在）
        if (!string.IsNullOrEmpty(welcomeMessage))
        {
            Console.WriteLine(welcomeMessage);
            Logger.LogInfo("Welcome message displayed");
        }
        
        Console.WriteLine(runningMessage);
        Logger.LogInfo($"Application running on localhost:{port}");
        
        // 自动打开默认浏览器
        var openUrl = $"http://localhost:{port}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = openUrl,
            UseShellExecute = true
        });
        Console.WriteLine(browserOpenedMessage);
        Logger.LogInfo($"Browser opened with URL: http://localhost:{port}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"无法自动打开浏览器: {ex.Message}");
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
        Console.WriteLine($"Error during shutdown logging: {ex.Message}");
        Logger.LogError($"Error during shutdown logging: {ex.Message}");
    }
});

app.Run();