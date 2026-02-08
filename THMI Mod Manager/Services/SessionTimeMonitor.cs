using System.Diagnostics;

namespace THMI_Mod_Manager.Services
{
    public class SessionTimeMonitor : BackgroundService
    {
        private readonly SessionTimeService _sessionTimeService;
        private const string PROCESS_NAME = "Touhou Mystia Izakaya";
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);

        public SessionTimeMonitor(SessionTimeService sessionTimeService)
        {
            _sessionTimeService = sessionTimeService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInfo("Session time monitor started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    CheckGameStatus();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Error checking game status");
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
                    Logger.LogInfo("Game process no longer running, stopping session time tracking");
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
                Logger.LogException(ex, "Error checking game process status");
                return false;
            }
        }
    }
}
