using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager.Middleware
{
    public class SetupWizardMiddleware
    {
        private readonly RequestDelegate _next;

        public SetupWizardMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, AppConfigManager appConfig)
        {
            var path = context.Request.Path.Value ?? "";

            if (path.StartsWith("/SetupWizard", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var isDevBuildStr = appConfig.Get("[Dev]IsDevBuild", "False");
            var isDevBuild = !string.IsNullOrEmpty(isDevBuildStr) && bool.Parse(isDevBuildStr);

            if (isDevBuild)
            {
                await _next(context);
                return;
            }

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

    public static class SetupWizardMiddlewareExtensions
    {
        public static IApplicationBuilder UseSetupWizard(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SetupWizardMiddleware>();
        }
    }
}
