using System.Diagnostics;

namespace THMI_Mod_Manager.Services
{
    public class SessionTimeMonitor : BackgroundService
    {
        private readonly SessionTimeService _sessionTimeService;
        private readonly ILogger<SessionTimeMonitor> _logger;
        private const string PROCESS_NAME = "Touhou Mystia Izakaya";
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);

        public SessionTimeMonitor(SessionTimeService sessionTimeService, ILogger<SessionTimeMonitor> logger)
        {
            _sessionTimeService = sessionTimeService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Session time monitor started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    CheckGameStatus();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking game status");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private void CheckGameStatus()
        {
            if (_sessionTimeService.IsRunning)
            {
                bool isGameRunning = IsGameProcessRunning();
                
                if (!isGameRunning)
                {
                    _logger.LogInformation("Game process no longer running, stopping session time tracking");
                    _sessionTimeService.StopSession();
                }
            }
        }

        private bool IsGameProcessRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName(PROCESS_NAME);
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking game process status");
                return false;
            }
        }
    }
}
