using System.Globalization;
using System.IO;
using System.Text;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager.Middleware
{
    /// <summary>
    /// Middleware for dynamic localization based on configuration / 基于配置的动态本地化中间件
    /// Sets the current culture based on the Language setting in AppConfig
    /// / 根据 AppConfig 中的语言设置设置当前文化
    /// </summary>
    public class LocalizationMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Constructor / 构造函数
        /// </summary>
        /// <param name="next">Next middleware in the pipeline / 管道中的下一个中间件</param>
        public LocalizationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Invoke the middleware / 调用中间件
        /// Reads the language from configuration and sets the current culture
        /// / 从配置中读取语言并设置当前文化
        /// </summary>
        /// <param name="context">HTTP context / HTTP 上下文</param>
        /// <param name="appConfig">App configuration manager / 应用程序配置管理器</param>
        public async Task InvokeAsync(HttpContext context, AppConfigManager appConfig)
        {
            // 从配置文件中获取语言设置 / Get language setting from configuration file
            var language = appConfig.Get("Localization", "Language", "en_US");
            
            // 确保language不为空 / Ensure language is not empty
            if (string.IsNullOrEmpty(language))
            {
                language = "en_US";
            }
            
            // 将语言设置转换为标准格式 / Convert language setting to standard format
            var cultureName = language.Replace('_', '-');
            
            try
            {
                var culture = new CultureInfo(cultureName);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
            catch
            {
                // 如果无法创建文化对象，则使用默认的英语 / Use default English if cannot create culture object
                var culture = new CultureInfo("en-US");
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
            
            await _next(context);
        }
    }
    
    /// <summary>
    /// Extension methods for LocalizationMiddleware / LocalizationMiddleware 中间件的扩展方法
    /// </summary>
    public static class LocalizationMiddlewareExtensions
    {
        /// <summary>
        /// Add LocalizationMiddleware to the application pipeline / 将 LocalizationMiddleware 添加到应用程序管道
        /// </summary>
        /// <param name="builder">Application builder / 应用程序构建器</param>
        /// <returns>Application builder for chaining / 用于链式调用的应用程序构建器</returns>
        public static IApplicationBuilder UseDynamicLocalization(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<LocalizationMiddleware>();
        }
    }
}