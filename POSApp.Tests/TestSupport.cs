using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Documents;

namespace POSApp.Tests
{
    /// <summary>
    /// Points the whole test run at a throw-away data folder before any test touches
    /// <see cref="POSApp.Core.Services.AppPaths"/>. Without this, tests that save region or
    /// receipt settings write into the real shop's %ProgramData% files on the developer's PC.
    /// </summary>
    internal static class TestEnvironment
    {
        public static string DataRoot { get; private set; } = string.Empty;

#pragma warning disable CA2255 // A module initializer is the only run-once hook xUnit v2 offers.
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Initialize()
        {
            DataRoot = Path.Combine(Path.GetTempPath(), "posapp-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DataRoot);
            Environment.SetEnvironmentVariable("POSAPP_DATA_DIR", DataRoot);

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try { Directory.Delete(DataRoot, recursive: true); } catch { /* temp folder */ }
            };
        }
    }

    /// <summary>
    /// Tests that change process-wide state — environment variables, the cached region and
    /// receipt settings, the app clock — run one at a time in this collection.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class GlobalStateCollection
    {
        public const string Name = "GlobalState";
    }

    /// <summary>WPF objects can only be created on an STA thread, which xUnit does not provide.</summary>
    internal static class Sta
    {
        public static T Run<T>(Func<T> func, CultureInfo? culture = null)
        {
            T result = default!;
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    if (culture != null)
                    {
                        Thread.CurrentThread.CurrentCulture = culture;
                        Thread.CurrentThread.CurrentUICulture = culture;
                    }
                    result = func();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("STA work did not finish within 30 seconds.");

            if (failure != null)
                throw new Xunit.Sdk.XunitException($"STA work failed: {failure}");
            return result;
        }

        public static void Run(Action action, CultureInfo? culture = null) =>
            Run(() => { action(); return 0; }, culture);

        /// <summary>Plain text of a printed document, as a cashier would read it off the paper.</summary>
        public static string TextOf(FlowDocument doc) =>
            new TextRange(doc.ContentStart, doc.ContentEnd).Text.Replace("\r\n", "\n");
    }
}
