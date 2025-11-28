using System.Globalization;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager.Middleware
{
    public class LocalizationMiddleware
    {
        private readonly RequestDelegate _next;

        public LocalizationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, AppConfigManager appConfig)
        {
            // 从配置文件中获取语言设置
            var language = appConfig.Get("Localization", "Language", "en_US");
            
            // 确保language不为空
            if (string.IsNullOrEmpty(language))
            {
                language = "en_US";
            }
            
            // 将语言设置转换为标准格式
            var cultureName = language.Replace('_', '-');
            
            try
            {
                var culture = new CultureInfo(cultureName);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
            catch
            {
                // 如果无法创建文化对象，则使用默认的英语
                var culture = new CultureInfo("en-US");
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }

            await _next(context);
        }
    }
    
    public static class LocalizationMiddlewareExtensions
    {
        public static IApplicationBuilder UseDynamicLocalization(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<LocalizationMiddleware>();
        }
    }
}