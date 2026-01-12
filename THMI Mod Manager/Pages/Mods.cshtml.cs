using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace THMI_Mod_Manager.Pages
{
    public class ModsModel : PageModel
    {
        private readonly ILogger<ModsModel> _logger;

        public ModsModel(ILogger<ModsModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            // 当前 Mod 管理功能已被 Mods.js 处理，此文件仅作为占位符。如果此文件不存在会导致编译错误。
            // 此页面的存在是为了满足 Razor Pages 的路由要求。

            // 當前 Mod 管理功能已被 Mods.js 處理，此文件僅作為占位符。如果此文件不存在會導致編譯錯誤。
            // 此頁面的存在是為了滿足 Razor Pages 的路由要求。

            // Current Mod Management Functionality is handled by Mods.js. This file is only a placeholder. If this file is missing, it will cause a compilation error.
            // This page exists to meet the routing requirements of Razor Pages.

            // 當前 Mod 管理機能は Mods.js によって処理されています。このファイルはプレースホルダーのみで、存在しない場合はコンパイルエラーが発生します。
            // このページは Razor Pages のルーティング要件を満たすために存在します。
        }
    }
}
