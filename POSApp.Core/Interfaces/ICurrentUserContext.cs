namespace POSApp.Core.Interfaces
{
    /// <summary>
    /// Abstraction over the currently authenticated user, so the service/data layer can
    /// enforce authorization without depending on the UI's session/static state. Implemented
    /// in the UI layer over SessionManager and injected into repositories that need to guard
    /// mutating operations below the UI (defense-in-depth, not just menu/button hiding).
    /// </summary>
    public interface ICurrentUserContext
    {
        int? UserId { get; }
        bool IsAuthenticated { get; }
        bool HasPermission(string permission);
    }
}
