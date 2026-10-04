using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace POSApp.Core.Services
{
    /// <summary>Why the Microsoft Store did not return a purchasable Swifttill Pro plan.</summary>
    public enum StorePlanProblem
    {
        None = 0,
        Network,
        NotSignedIn,
        StoreUnavailable,
        CatalogEmpty,
        Other
    }

    /// <summary>
    /// Maps a Store query outcome to a cause. Pure: no WinRT, so the upgrade dialog and the
    /// tests can agree on the same messages without talking to the Store.
    /// </summary>
    public static class StorePlanClassifier
    {
        /// <summary>ERROR_NO_SUCH_USER. StoreContext sets this when nobody is signed in to the Store.</summary>
        public const int ErrorNoSuchUser = unchecked((int)0x80070525);

        /// <summary>Generic Store failure. The code alone does not say why, so the dialog shows it.</summary>
        public const int StoreUnexpected = unchecked((int)0x803F6107);

        /// <summary>The package has no Store licence the add-on query can use (sideload, or not published for this account).</summary>
        public const int StoreLicenseMissing = unchecked((int)0x803F6108);

        public const int ClassNotRegistered = unchecked((int)0x80040154);
        public const int ServiceDisabled = unchecked((int)0x80070422);
        public const int ServiceNotActive = unchecked((int)0x80070426);

        private static readonly int[] NetworkHResults =
        {
            unchecked((int)0x80072EE2), // ERROR_INTERNET_TIMEOUT
            unchecked((int)0x80072EE7), // ERROR_INTERNET_NAME_NOT_RESOLVED
            unchecked((int)0x80072EFD), // ERROR_INTERNET_CANNOT_CONNECT
            unchecked((int)0x80072EFE), // ERROR_INTERNET_CONNECTION_ABORTED
            unchecked((int)0x80072EFF), // ERROR_INTERNET_CONNECTION_RESET
            unchecked((int)0x80072F83), // ERROR_INTERNET_DISCONNECTED
            unchecked((int)0x80072F84), // ERROR_INTERNET_SERVER_UNREACHABLE
            unchecked((int)0x80072F85), // ERROR_INTERNET_PROXY_SERVER_UNREACHABLE
            unchecked((int)0x800704CF)  // ERROR_NETWORK_UNREACHABLE
        };

        /// <summary>
        /// <paramref name="purchasableCount"/> wins: a plan the user can buy hides the error.
        /// <paramref name="networkAvailable"/> is only used when the Store gave no specific cause
        /// (empty success, or the generic <see cref="StoreUnexpected"/> code) so an offline PC
        /// is not described as an empty catalog.
        /// </summary>
        public static StorePlanProblem Classify(int? hresult, int purchasableCount, bool? networkAvailable = null)
        {
            if (purchasableCount > 0) return StorePlanProblem.None;

            if (hresult is int code)
            {
                var problem = FromHResult(code);
                if (problem == StorePlanProblem.Other && code == StoreUnexpected && networkAvailable == false)
                    return StorePlanProblem.Network;
                return problem;
            }

            if (networkAvailable == false) return StorePlanProblem.Network;
            return StorePlanProblem.CatalogEmpty;
        }

        public static StorePlanProblem FromHResult(int hresult)
        {
            if (hresult == ErrorNoSuchUser) return StorePlanProblem.NotSignedIn;
            if (hresult == StoreLicenseMissing) return StorePlanProblem.CatalogEmpty;
            if (hresult == ClassNotRegistered || hresult == ServiceDisabled || hresult == ServiceNotActive)
                return StorePlanProblem.StoreUnavailable;
            if (IsNetwork(hresult)) return StorePlanProblem.Network;
            return StorePlanProblem.Other;
        }

        public static StorePlanProblem FromException(Exception ex)
        {
            for (Exception? current = ex; current != null; current = current.InnerException)
            {
                if (current is HttpRequestException or WebException)
                    return StorePlanProblem.Network;
                if (current.HResult == ClassNotRegistered)
                    return StorePlanProblem.StoreUnavailable;
            }

            return FromHResult(ex.HResult);
        }

        public static string FormatCode(int hresult) => $"0x{hresult:X8}";

        private static bool IsNetwork(int hresult)
        {
            foreach (var code in NetworkHResults)
                if (code == hresult) return true;
            return false;
        }
    }

    /// <summary>Sentences the upgrade dialog shows. The feature list is not one of these.</summary>
    public static class StorePlanMessages
    {
        public const string Network = "No internet connection. Reconnect, then try again.";
        public const string NotSignedIn = "Sign in to the Microsoft Store with the account that should own this subscription, then try again.";
        public const string StoreUnavailable = "Microsoft Store is not available on this PC. Install or open it, then try again.";
        public const string CatalogEmpty = "No Swiftill Pro plan is available for this account yet. The add-on may still be publishing, or it is not offered in this region.";
        public const string Other = "The Microsoft Store could not load plans.";

        public static string For(StorePlanProblem problem) => problem switch
        {
            StorePlanProblem.Network => Network,
            StorePlanProblem.NotSignedIn => NotSignedIn,
            StorePlanProblem.StoreUnavailable => StoreUnavailable,
            StorePlanProblem.CatalogEmpty => CatalogEmpty,
            StorePlanProblem.Other => Other,
            _ => ""
        };

        public static string ErrorCodeLine(string errorCode) => "Error code " + errorCode;
    }

    /// <summary>
    /// The one support contact for a Store catalog that did not load. The number is the
    /// international form of the shop WhatsApp (local 0313-7643443). Opening the link does
    /// not unlock Pro.
    /// </summary>
    public static class StoreSupportContact
    {
        public const string WhatsAppNumber = "923137643443";
        public const string PlansDidNotLoadText = "Swiftill Pro plans did not load";

        public static Uri PlansDidNotLoadLink { get; } = new(
            "https://wa.me/" + WhatsAppNumber + "?text=" + Uri.EscapeDataString(PlansDidNotLoadText));
    }

    /// <summary>Joins a Store price and its billing period without appending the period twice.</summary>
    public static class StorePriceText
    {
        public static string Format(string? formattedRecurrencePrice, string? formattedPrice, string? billingPeriod)
        {
            if (!string.IsNullOrWhiteSpace(formattedRecurrencePrice))
                return formattedRecurrencePrice.Trim();

            if (string.IsNullOrWhiteSpace(formattedPrice)) return "";

            var price = formattedPrice.Trim();
            if (string.IsNullOrWhiteSpace(billingPeriod) || price.Contains('/'))
                return price;

            return price + "/" + billingPeriod.Trim();
        }
    }
}
