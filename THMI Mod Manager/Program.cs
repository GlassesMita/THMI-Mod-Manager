using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using THMI_Mod_Manager.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

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

app.Run();