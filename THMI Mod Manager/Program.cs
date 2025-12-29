using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.Server;
using THMI_Mod_Manager.Middleware;
using System.Diagnostics;
using THMI_Mod_Manager;

// 简单的权限检查 - 仅使用管理员权限运行
Console.WriteLine("正在检查权限状态...");

// 检查是否以管理员身份运行
bool isAdmin = PermissionHelper.IsRunAsAdmin();
Console.WriteLine($"管理员权限: {isAdmin}");

if (!isAdmin)
{
    Console.WriteLine("需要管理员权限，正在重新启动...");
    PermissionHelper.RestartAsAdministrator();
    Environment.Exit(0);
}

Console.WriteLine("已获得管理员权限，继续启动应用...");

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
int portToUse = 5500;

if (!string.IsNullOrEmpty(configuredUrls))
{
    // 如果配置了URL，尝试解析端口
    var url = configuredUrls.Split(';').FirstOrDefault() ?? "http://localhost:5500";
    var uri = new Uri(url);
    portToUse = uri.Port;
    
    // 如果端口是80或0，使用5500作为默认值
    if (portToUse == 80 || portToUse == 0)
    {
        portToUse = 5500;
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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers(); // 添加API控制器支持

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
        var port = 5500;
        
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
        string runningMessage = $"正在 localhost:{port} 上运行"; // 默认消息
        string browserOpenedMessage = $"已自动打开浏览器: http://localhost:{port}"; // 默认消息
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
            }
        }
        
        // 先输出欢迎消息（如果存在）
        if (!string.IsNullOrEmpty(welcomeMessage))
        {
            Console.WriteLine(welcomeMessage);
        }
        
        Console.WriteLine(runningMessage);
        
        // 自动打开默认浏览器
        var openUrl = $"http://localhost:{port}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = openUrl,
            UseShellExecute = true
        });
        Console.WriteLine(browserOpenedMessage);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"无法自动打开浏览器: {ex.Message}");
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
    }
});

app.Run();