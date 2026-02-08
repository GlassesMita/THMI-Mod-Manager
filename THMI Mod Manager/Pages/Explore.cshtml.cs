using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager.Pages
{
    public class ExploreModel : PageModel
    {
        private readonly THMI_Mod_Manager.Services.AppConfigManager _appConfig;

        public ExploreModel(THMI_Mod_Manager.Services.AppConfigManager appConfig)
        {
            _appConfig = appConfig;
        }

        public void OnGet()
        {
            // 占位符页面。生产环境不存在，于是这个页面对应的东西也就不存在。要是这个文件缺失的话会导致一些错误。

            // 佔位符頁面。生產環境不存在，于是這個頁面对應的東西也就不存在。要是這個文件缺失的話會導致一些錯誤。

            // Placeholder page. This page should not exist in production environment. If this file is missing, it will cause some errors.

            // 占位符ページ。本番環境では存在しないはずです。このファイルが欠落していると、エラーが発生する可能性があります。
        }
    }
}
