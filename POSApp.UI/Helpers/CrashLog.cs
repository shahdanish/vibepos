using System.IO;
using System.Windows;
using System.Windows.Threading;
using POSApp.Core.Services;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// Last line of defence: any error nothing else caught is written to logs\errors.log and the
    /// cashier sees a plain message instead of the program disappearing mid-sale. Saved data is
    /// never touched; the action that failed simply did not finish.
    /// </summary>
    public static class CrashLog
    {
        private const long MaxLogBytes = 2 * 1024 * 1024;
        private static readonly object Gate = new();
        private static DateTime _lastShown = DateTime.MinValue;

        public static string FilePath => Path.Combine(AppPaths.LogsDirectory, "errors.log");

        public static void Install(Application app)
        {
            app.DispatcherUnhandledException += OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Write(e.ExceptionObject as Exception, "AppDomain" + (e.IsTerminating ? " (terminating)" : ""));
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Write(e.Exception, "Background task");
                e.SetObserved();
            };
        }

        private static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Write(e.Exception, "UI");
            e.Handled = true;

            // One message at a time, so an error that repeats can't bury the screen in pop-ups.
            if ((DateTime.Now - _lastShown).TotalSeconds < 5) return;
            _lastShown = DateTime.Now;
            MessageBox.Show(
                "Something went wrong and that action could not be finished.\n\n" +
                "Everything already saved is safe. If it keeps happening, send this file to support:\n" + FilePath,
                "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>Appends one error with its full details. Never throws.</summary>
        public static void Write(Exception? ex, string source)
        {
            if (ex == null) return;
            try
            {
                lock (Gate)
                {
                    var path = FilePath;
                    if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
                        File.Move(path, path + ".old", overwrite: true);
                    var version = typeof(CrashLog).Assembly.GetName().Version;
                    File.AppendAllText(path,
                        $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} · {source} · v{version}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
                }
            }
            catch
            {
                // Logging must never cause a second failure.
            }
        }
    }
}
