using System.Printing;
using POSApp.Infrastructure.Services;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// Opens the cash drawer wired to the receipt printer, when this PC has it switched on
    /// (Business Settings → Devices). Off by default, so nothing happens on tills without one.
    /// </summary>
    public static class CashDrawer
    {
        public static bool IsEnabled => SettingsManager.LoadSettings().CashDrawerEnabled;

        /// <summary>Opens the drawer if it is switched on. Returns null on success or when off, else the problem.</summary>
        public static string? Open()
        {
            var s = SettingsManager.LoadSettings();
            if (!s.CashDrawerEnabled) return null;
            return OpenOn(s.CashDrawerPrinter, s.CashDrawerPin5);
        }

        /// <summary>Opens the drawer on a given printer (used by the Test button). Empty name = default printer.</summary>
        public static string? OpenOn(string? printerName, bool pin5)
        {
            try
            {
                var printer = string.IsNullOrWhiteSpace(printerName) ? DefaultPrinterName() : printerName;
                if (printer == null) return "There is no default printer on this PC.";
                var error = RawPrinter.Send(printer, RawPrinter.DrawerKick(pin5));
                if (error != null) CrashLog.Write(new InvalidOperationException(error), "Cash drawer");
                return error;
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex, "Cash drawer");
                return ex.Message;
            }
        }

        /// <summary>Printers installed on this PC, for the Devices tab.</summary>
        public static IReadOnlyList<string> InstalledPrinters()
        {
            try
            {
                using var server = new LocalPrintServer();
                return server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections })
                             .Select(q => q.FullName).OrderBy(n => n).ToList();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string? DefaultPrinterName()
        {
            try { return LocalPrintServer.GetDefaultPrintQueue()?.FullName; }
            catch { return null; }
        }
    }
}
