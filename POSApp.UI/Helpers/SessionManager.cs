using POSApp.Core.Entities;

namespace POSApp.UI.Helpers
{
    public static class SessionManager
    {
        private static User? _currentUser;
        private static HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);

        public static bool IsLoggedIn => _currentUser != null;

        public static User? CurrentUser => _currentUser;

        public static bool IsAdmin => HasPermission(Permissions.UsersManage);

        public static void SetSession(User user, IEnumerable<string> permissions)
        {
            _currentUser = user;
            _permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
        }

        public static bool HasPermission(string permission)
            => _permissions.Contains(permission);

        public static void Logout()
        {
            _currentUser = null;
            _permissions.Clear();
        }
    }
}
