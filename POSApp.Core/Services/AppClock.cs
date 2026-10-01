namespace POSApp.Core.Services
{
    /// <summary>
    /// The app's notion of "now". Production code reads the system clock; tests (and a future
    /// screenshot mode) swap in a fixed <see cref="TimeProvider"/>.
    /// </summary>
    public static class AppClock
    {
        public static TimeProvider Provider { get; set; } = TimeProvider.System;

        /// <summary>Local wall-clock time.</summary>
        public static DateTime Now => Provider.GetLocalNow().DateTime;
    }
}
