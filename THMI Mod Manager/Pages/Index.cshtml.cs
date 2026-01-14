using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using Markdig;
using System.Reflection;

namespace THMI_Mod_Manager.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly THMI_Mod_Manager.Services.AppConfigManager _appConfig;
        private readonly IWebHostEnvironment _env;

        public new string Content { get; set; } = string.Empty;

        public IndexModel(ILogger<IndexModel> logger, THMI_Mod_Manager.Services.AppConfigManager appConfig, IWebHostEnvironment env)
        {
            _logger = logger;
            _appConfig = appConfig;
            _env = env;
        }

        public void OnGet()
        {
            // Get the current language
            var language = _appConfig.Get("[Localization]Language", "en_US");
            
            // Try to find a localized markdown file
            var basePath = Path.Combine(_env.ContentRootPath, "Localization");
            var markdownFile = Path.Combine(basePath, $"Home.{language}.md");
            
            // Fallback to default language if specific language file doesn't exist
            if (!System.IO.File.Exists(markdownFile))
            {
                markdownFile = Path.Combine(basePath, "Home.en_US.md");
                
                // If even default doesn't exist, try without language specifier
                if (!System.IO.File.Exists(markdownFile))
                {
                    markdownFile = Path.Combine(basePath, "Home.md");
                    
                    // If no markdown file exists, provide default content
                    if (!System.IO.File.Exists(markdownFile))
                    {
                        Content = GetDefaultContent(language ?? "en_US");
                        return;
                    }
                }
            }
            
            try
            {
                // Read the markdown file
                var markdown = System.IO.File.ReadAllText(markdownFile, Encoding.UTF8);
                
                // Process ASP.NET-like code blocks (e.g., @year)
                markdown = ProcessAspNetCode(markdown);
                
                // Convert markdown to HTML
                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                Content = Markdown.ToHtml(markdown, pipeline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading or parsing markdown file: {FileName}", markdownFile);
                Content = GetErrorContent(language ?? "en_US");
            }
        }
        
        private string GetDefaultContent(string language)
        {
            if (language.StartsWith("zh"))
            {
                return @"<h2>欢迎使用 THMI 模组管理器</h2>
<p>这是一个用于管理东方Project模组的工具。</p>";
            }
            else if (language.StartsWith("ja"))
            {
                return @"<h2>THMI Mod Managerへようこそ</h2>
<p>これは東方ProjectのModを管理するためのツールです。</p>";
            }
            else
            {
                return @"<h2>Welcome to THMI Mod Manager</h2>
<p>This is a tool for managing Touhou Project mods.</p>";
            }
        }
        
        private string GetErrorContent(string language)
        {
            if (language.StartsWith("zh"))
            {
                return @"<p class=""text-danger"">加载主页内容时出错。</p>";
            }
            else if (language.StartsWith("ja"))
            {
                return @"<p class=""text-danger"">ホームページコンテンツの読み込み中にエラーが発生しました。</p>";
            }
            else
            {
                return @"<p class=""text-danger"">Error loading home page content.</p>";
            }
        }
        
        private string ProcessAspNetCode(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return markdown;
                
            // Replace @year with current year
            markdown = markdown.Replace("@year", DateTime.Now.Year.ToString());
            
            // Replace @versionCode with app version code from AppConfig.Schale (do this first to avoid conflicts)
            var versionCode = _appConfig.Get("[App]VersionCode", "1");
            markdown = markdown.Replace("@versionCode", versionCode);
            
            // Replace @version with app version from assembly
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
            markdown = markdown.Replace("@version", assemblyVersion);
            
            // Replace @appName with localized app name
            var appName = _appConfig.GetLocalized("App:Name", "THMI Mod Manager");
            markdown = markdown.Replace("@appName", appName);
            
            // Add more replacements as needed
            
            return markdown;
        }
    }
}
