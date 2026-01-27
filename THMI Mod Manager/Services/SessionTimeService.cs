using System.Diagnostics;

namespace THMI_Mod_Manager.Services
{
    public class SessionTimeService
    {
        private DateTime? _sessionStartTime;
        private TimeSpan _accumulatedTime;
        private bool _isRunning;
        private readonly object _lock = new object();

        public DateTime? SessionStartTime => _sessionStartTime;

        public bool IsRunning => _isRunning;

        public void StartSession()
        {
            lock (_lock)
            {
                if (!_isRunning)
                {
                    _sessionStartTime = DateTime.Now;
                    _accumulatedTime = TimeSpan.Zero;
                    _isRunning = true;
                }
            }
        }

        public void StopSession()
        {
            lock (_lock)
            {
                if (_isRunning && _sessionStartTime.HasValue)
                {
                    _accumulatedTime += DateTime.Now - _sessionStartTime.Value;
                    _sessionStartTime = null;
                    _isRunning = false;
                }
            }
        }

        public void ResetSession()
        {
            lock (_lock)
            {
                _sessionStartTime = null;
                _accumulatedTime = TimeSpan.Zero;
                _isRunning = false;
            }
        }

        public TimeSpan GetCurrentSessionTime()
        {
            lock (_lock)
            {
                if (_isRunning && _sessionStartTime.HasValue)
                {
                    return _accumulatedTime + (DateTime.Now - _sessionStartTime.Value);
                }
                return _accumulatedTime;
            }
        }

        public string GetFormattedTime()
        {
            var timeSpan = GetCurrentSessionTime();
            var totalDays = timeSpan.TotalDays;
            var hours = timeSpan.Hours;
            var minutes = timeSpan.Minutes;
            var seconds = timeSpan.Seconds;

            if (totalDays >= 1)
            {
                var days = (int)totalDays;
                var remainingHours = timeSpan.Hours;
                return $"{days}:{remainingHours:D2}:{minutes:D2}:{seconds:D2}";
            }
            else
            {
                return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            }
        }

        public SessionState GetState()
        {
            lock (_lock)
            {
                return new SessionState
                {
                    IsRunning = _isRunning,
                    FormattedTime = GetFormattedTime(),
                    TotalSeconds = GetCurrentSessionTime().TotalSeconds
                };
            }
        }
    }

    public class SessionState
    {
        public bool IsRunning { get; set; }
        public string FormattedTime { get; set; } = "00:00:00";
        public double TotalSeconds { get; set; }
    }
}
