using System.Runtime.InteropServices;

namespace POSApp.Core.Services
{
    /// <summary>
    /// Facts about how the running process was installed. The only thing that matters today is
    /// whether we run inside an MSIX package (Microsoft Store / sideloaded) or as a classic
    /// Win32 install (Inno Setup / dotnet run).
    /// </summary>
    public static class AppEnvironment
    {
        private const int AppModelErrorNoPackage = 15700;

        private static readonly Lazy<bool> _isPackaged = new(DetectPackaged);

        /// <summary>True when the process has MSIX package identity.</summary>
        public static bool IsPackaged => _isPackaged.Value;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

        private static bool DetectPackaged()
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(8)) return false;
            try
            {
                var length = 0;
                return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }
    }
}
