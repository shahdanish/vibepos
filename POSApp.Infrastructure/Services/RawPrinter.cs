using System.Runtime.InteropServices;

namespace POSApp.Infrastructure.Services
{
    /// <summary>
    /// Sends raw bytes straight to a Windows printer (no driver rendering). Used for receipt
    /// printer commands such as opening the cash drawer that is plugged into the printer.
    /// </summary>
    public static class RawPrinter
    {
        /// <summary>
        /// ESC p m t1 t2: pulse the drawer kick connector. Pin 2 is the usual one; some drawers
        /// are wired to pin 5. On for 50 ms, off for 500 ms.
        /// </summary>
        public static byte[] DrawerKick(bool pin5 = false) => new byte[] { 0x1B, 0x70, (byte)(pin5 ? 1 : 0), 0x19, 0xFA };

        /// <summary>Sends <paramref name="data"/> to <paramref name="printerName"/>. Returns null on success, else why it failed.</summary>
        public static string? Send(string printerName, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(printerName)) return "No printer is set.";
            if (!OpenPrinter(printerName, out var handle, IntPtr.Zero))
                return $"Could not open printer '{printerName}' (error {Marshal.GetLastWin32Error()}).";
            try
            {
                var doc = new DocInfo { DocName = "Cash drawer", DataType = "RAW" };
                if (!StartDocPrinter(handle, 1, doc)) return $"The printer did not accept the job (error {Marshal.GetLastWin32Error()}).";
                try
                {
                    if (!StartPagePrinter(handle)) return $"The printer did not start (error {Marshal.GetLastWin32Error()}).";
                    var buffer = Marshal.AllocCoTaskMem(data.Length);
                    try
                    {
                        Marshal.Copy(data, 0, buffer, data.Length);
                        if (!WritePrinter(handle, buffer, data.Length, out var written) || written != data.Length)
                            return $"Sending to the printer failed (error {Marshal.GetLastWin32Error()}).";
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(buffer);
                        EndPagePrinter(handle);
                    }
                }
                finally
                {
                    EndDocPrinter(handle);
                }
                return null;
            }
            finally
            {
                ClosePrinter(handle);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class DocInfo
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string? DocName;
            [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string? DataType;
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool OpenPrinter(string printerName, out IntPtr handle, IntPtr defaults);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr handle);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool StartDocPrinter(IntPtr handle, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DocInfo doc);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr handle);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr handle);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr handle);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr handle, IntPtr bytes, int count, out int written);
    }
}
