using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace THMI_Mod_Manager.Pages
{
    public class WhatsNewModel : PageModel
    {
        private readonly IStringLocalizer<WhatsNewModel> _localizer;

        public WhatsNewModel(IStringLocalizer<WhatsNewModel> localizer)
        {
            _localizer = localizer;
        }

        public void OnGet()
        {
        }
    }
}
