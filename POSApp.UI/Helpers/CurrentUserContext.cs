using POSApp.Core.Interfaces;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// UI-layer implementation of <see cref="ICurrentUserContext"/> backed by the static
    /// <see cref="SessionManager"/>. Registered as a singleton and injected into repositories
    /// so the data layer can enforce permissions without referencing the UI directly.
    /// </summary>
    public sealed class CurrentUserContext : ICurrentUserContext
    {
        public int? UserId => SessionManager.CurrentUser?.Id;

        public bool IsAuthenticated => SessionManager.IsLoggedIn;

        public bool HasPermission(string permission) => SessionManager.HasPermission(permission);
    }
}
