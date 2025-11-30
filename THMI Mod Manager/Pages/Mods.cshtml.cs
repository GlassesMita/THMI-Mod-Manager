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
            // 当前生产环境还没有实际可用的 Mod
            // 此页面仅作为未来功能的占位符
        }
    }
}