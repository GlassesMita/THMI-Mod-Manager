using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager.Middleware
{
    /// <summary>
    /// Middleware to handle first-run setup wizard / 处理首次运行设置向导的中间件
    /// Redirects to SetupWizard if it's the first run of the application
    /// / 如果是应用程序首次运行则重定向到SetupWizard
    /// </summary>
    public class SetupWizardMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Constructor / 构造函数
        /// </summary>
        /// <param name="next">Next middleware in the pipeline / 管道中的下一个中间件</param>
        public SetupWizardMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Invoke the middleware / 调用中间件
        /// Checks if it's the first run and redirects to SetupWizard if needed
        /// / 检查是否是首次运行并在需要时重定向到SetupWizard
        /// </summary>
        /// <param name="context">HTTP context / HTTP 上下文</param>
        /// <param name="appConfig">App configuration manager / 应用程序配置管理器</param>
        public async Task InvokeAsync(HttpContext context, AppConfigManager appConfig)
        {
            var path = context.Request.Path.Value ?? "";

            // Skip if already on SetupWizard page / 如果已在 SetupWizard 页面则跳过
            if (path.StartsWith("/SetupWizard", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Skip if it's a dev build / 如果是开发版本则跳过
            var isDevBuildStr = appConfig.Get("[Dev]IsDevBuild", "False");
            var isDevBuild = !string.IsNullOrEmpty(isDevBuildStr) && bool.Parse(isDevBuildStr);

            if (isDevBuild)
            {
                await _next(context);
                return;
            }

            // Check if it's first run / 检查是否是首次运行
            var isFirstRunStr = appConfig.Get("[App]IsFirstRun", "True");
            var isFirstRun = !string.IsNullOrEmpty(isFirstRunStr) && bool.Parse(isFirstRunStr);

            if (isFirstRun)
            {
                context.Response.Redirect("/SetupWizard");
                return;
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Extension methods for SetupWizardMiddleware / SetupWizardMiddleware 的扩展方法
    /// </summary>
    public static class SetupWizardMiddlewareExtensions
    {
        /// <summary>
        /// Add SetupWizardMiddleware to the application pipeline / 将 SetupWizardMiddleware 添加到应用程序管道
        /// </summary>
        /// <param name="builder">Application builder / 应用程序构建器</param>
        /// <returns>Application builder for chaining / 用于链式调用的应用程序构建器</returns>
        public static IApplicationBuilder UseSetupWizard(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SetupWizardMiddleware>();
        }
    }
}
