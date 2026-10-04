using POSApp.Core.Services;

namespace POSApp.Core.Interfaces
{
    /// <summary>
    /// Tells the app which edition it is and whether a feature is unlocked. In the Store build
    /// it also talks to the Microsoft Store to check and buy the "Pro" subscription.
    /// </summary>
    public interface IEditionService
    {
        AppEdition Edition { get; }

        bool IsEnabled(AppFeature feature);

        bool IsInBuild(AppFeature feature);

        /// <summary>When the current Pro subscription period ends (null if not subscribed / unknown).</summary>
        DateTimeOffset? ProExpiresOn { get; }

        /// <summary>Raised when the edition changes (purchase, renewal lapse, refund).</summary>
        event EventHandler? EditionChanged;

        /// <summary>Re-reads the Store licence. No-op for the direct edition.</summary>
        Task RefreshAsync();

        /// <summary>
        /// The Pro subscription add-ons on sale, with Store-formatted prices, or the reason none came back.
        /// <paramref name="ownerWindowHandle"/> is the HWND the Store uses if it needs to prompt.
        /// </summary>
        Task<ProOfferQuery> GetProOffersAsync(IntPtr ownerWindowHandle);

        /// <summary>
        /// Shows the Store purchase dialog for <paramref name="offerId"/> (a <see cref="ProOffer.Id"/>).
        /// <paramref name="ownerWindowHandle"/> is the HWND the Store dialog is parented to.
        /// </summary>
        Task<UpgradeResult> RequestUpgradeAsync(IntPtr ownerWindowHandle, string offerId);
    }

    /// <summary>One purchasable Pro plan.</summary>
    /// <param name="Id">Store ID of the add-on (opaque to callers).</param>
    /// <param name="Title">Plan name as entered in Partner Center, e.g. "Swifttill Pro — Monthly".</param>
    /// <param name="PriceText">Localised recurring price, e.g. "Rs 1,500.00/month".</param>
    /// <param name="TrialText">e.g. "1 month free trial", or null when there is no trial.</param>
    public sealed record ProOffer(string Id, string Title, string PriceText, string? TrialText);

    /// <summary>
    /// Result of asking the Store for Pro plans.
    /// <paramref name="ErrorCode"/> is the HRESULT (for example 0x80070525) when the Store reported one.
    /// The dialog shows it only for <see cref="StorePlanProblem.Other"/>.
    /// </summary>
    public sealed record ProOfferQuery(
        IReadOnlyList<ProOffer> Offers,
        StorePlanProblem Problem,
        string? ErrorCode);

    public enum UpgradeResult
    {
        Purchased,
        AlreadyOwned,
        Cancelled,
        NotAvailable,
        Failed
    }
}
