using System.Net.NetworkInformation;

namespace POSApp.Infrastructure.Services
{
    /// <summary>
    /// Detects internet connectivity by pinging a reliable host.
    /// Raises ConnectivityChanged event when state changes.
    /// </summary>
    public sealed class ConnectivityDetector : IDisposable
    {
        private readonly TimeSpan _checkInterval;
        private readonly CancellationTokenSource _cts = new();
        private bool _isOnline;
        private Task? _loopTask;

        public bool IsOnline => _isOnline;

        public event EventHandler<bool>? ConnectivityChanged;

        public ConnectivityDetector(TimeSpan? checkInterval = null)
        {
            _checkInterval = checkInterval ?? TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Start the background connectivity check loop.
        /// </summary>
        public void Start()
        {
            if (_loopTask is not null) return;
            _loopTask = Task.Run(CheckLoopAsync);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        private async Task CheckLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                var wasOnline = _isOnline;
                _isOnline = await CheckConnectivityAsync();

                if (wasOnline != _isOnline)
                {
                    ConnectivityChanged?.Invoke(this, _isOnline);
                }

                try
                {
                    await Task.Delay(_checkInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        /// <summary>
        /// Check connectivity by attempting an HTTP HEAD request to Google/Firebase.
        /// More reliable than Ping which can be blocked by firewalls.
        /// </summary>
        private static async Task<bool> CheckConnectivityAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                // Lightweight connectivity check — any 200-level response means we're online
                var response = await client.GetAsync("https://www.google.com/generate_204");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
