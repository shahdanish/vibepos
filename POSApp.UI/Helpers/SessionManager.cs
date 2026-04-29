using POSApp.Core.Entities;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// Manages user session and authentication state
    /// </summary>
    public static class SessionManager
    {
        private static User? _currentUser;

        public static bool IsLoggedIn => _currentUser != null;

        public static User? CurrentUser
        {
            get => _currentUser;
            set => _currentUser = value;
        }

        public static bool IsAdmin => _currentUser?.Role == "Admin";

        public static void Logout()
        {
            _currentUser = null;
        }
    }
}
