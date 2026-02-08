using Microsoft.AspNetCore.Mvc;
using THMI_Mod_Manager.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace THMI_Mod_Manager.ViewComponents
{
    public class LaunchButtonViewComponent : ViewComponent
    {
        private readonly AppConfigManager _appConfig;

        public LaunchButtonViewComponent(AppConfigManager appConfig)
        {
            _appConfig = appConfig;
        }

        public IViewComponentResult Invoke()
        {
            var model = new LaunchButtonViewModel
            {
                IsProcessRunning = IsProcessRunningCheck(),
                SteamAppId = "1584090",
                ProcessName = "Touhou Mystia Izakaya",
                ConfirmStopTitle = _appConfig.GetLocalized("Buttons:ConfirmStopTitle", "Confirm Stop"),
                ConfirmStopMessage = _appConfig.GetLocalized("Buttons:ConfirmStopMessage", "Forcefully stopping the game may cause unsaved progress to be lost.\n\nAre you sure you want to stop the game?"),
                LaunchText = _appConfig.GetLocalized("Buttons:Launch", "Launch"),
                StopText = _appConfig.GetLocalized("Buttons:Stop", "Stop"),
                CancelText = _appConfig.GetLocalized("Common:Cancel", "Cancel"),
                ConfirmText = _appConfig.GetLocalized("Common:Confirm", "Confirm")
            };

            // 设置按钮状态
            if (model.IsProcessRunning)
            {
                model.ButtonText = _appConfig.GetLocalized("Buttons:Stop", "Stop");
                model.ButtonIcon = "icon-stop";
                model.ButtonClass = "btn-danger";
            }
            else
            {
                model.ButtonText = _appConfig.GetLocalized("Buttons:Launch", "Launch");
                model.ButtonIcon = "icon-play";
                model.ButtonClass = "btn-success";
            }

            return View(model);
        }

        private bool IsProcessRunningCheck()
        {
            try
            {
                var processes = Process.GetProcessesByName("Touhou Mystia Izakaya");
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "检查进程状态时出错");
                return false;
            }
        }
    }

    public class LaunchButtonViewModel
    {
        public string ButtonText { get; set; } = "Launch";
        public string ButtonIcon { get; set; } = "bi-play-fill";
        public string ButtonClass { get; set; } = "btn-success";
        public bool IsProcessRunning { get; set; }
        public string SteamAppId { get; set; } = "1584090";
        public string ProcessName { get; set; } = "Touhou Mystia Izakaya";
        public string ConfirmStopTitle { get; set; } = "Confirm Stop";
        public string ConfirmStopMessage { get; set; } = "Forcefully stopping the game may cause unsaved progress to be lost.\n\nAre you sure you want to stop the game?";
        public string LaunchText { get; set; } = "Launch";
        public string StopText { get; set; } = "Stop";
        public string CancelText { get; set; } = "Cancel";
        public string ConfirmText { get; set; } = "Confirm";
    }
}