using Microsoft.AspNetCore.Mvc;
using THMI_Mod_Manager.Services;

namespace THMI_Mod_Manager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        [HttpGet("poll")]
        public IActionResult PollNotifications()
        {
            var notifications = Logger.GetPendingNotifications();
            return Ok(new { success = true, data = notifications });
        }

        [HttpPost("clear")]
        public IActionResult ClearNotifications()
        {
            Logger.ClearNotifications();
            return Ok(new { success = true, message = "Notifications cleared" });
        }
    }
}
