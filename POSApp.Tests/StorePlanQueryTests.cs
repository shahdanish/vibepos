using System.Net.Http;
using System.Runtime.InteropServices;
using POSApp.Core.Services;
using POSApp.Infrastructure.Services;

namespace POSApp.Tests
{
    public class StorePlanQueryTests
    {
        [Fact]
        public void PlansPresent_HidesTheError()
        {
            Assert.Equal(StorePlanProblem.None, StorePlanClassifier.Classify(null, 2));
            Assert.Equal(StorePlanProblem.None, StorePlanClassifier.Classify(StorePlanClassifier.ErrorNoSuchUser, 1, false));
            Assert.Equal(StorePlanProblem.None, StorePlanClassifier.Classify(unchecked((int)0x80072EE7), 1));
        }

        [Fact]
        public void EmptySuccess_IsAnEmptyCatalog_UnlessThePcIsOffline()
        {
            Assert.Equal(StorePlanProblem.CatalogEmpty, StorePlanClassifier.Classify(null, 0));
            Assert.Equal(StorePlanProblem.CatalogEmpty, StorePlanClassifier.Classify(null, 0, true));
            Assert.Equal(StorePlanProblem.Network, StorePlanClassifier.Classify(null, 0, false));
        }

        [Theory]
        [InlineData(0x80070525, StorePlanProblem.NotSignedIn)]
        [InlineData(0x80072EE2, StorePlanProblem.Network)]
        [InlineData(0x80072EE7, StorePlanProblem.Network)]
        [InlineData(0x80072EFD, StorePlanProblem.Network)]
        [InlineData(0x80072EFE, StorePlanProblem.Network)]
        [InlineData(0x80072EFF, StorePlanProblem.Network)]
        [InlineData(0x80072F83, StorePlanProblem.Network)]
        [InlineData(0x80072F84, StorePlanProblem.Network)]
        [InlineData(0x800704CF, StorePlanProblem.Network)]
        [InlineData(0x80040154, StorePlanProblem.StoreUnavailable)]
        [InlineData(0x80070422, StorePlanProblem.StoreUnavailable)]
        [InlineData(0x80070426, StorePlanProblem.StoreUnavailable)]
        [InlineData(0x803F6108, StorePlanProblem.CatalogEmpty)]
        [InlineData(0x803F6107, StorePlanProblem.Other)]
        [InlineData(0x80004005, StorePlanProblem.Other)]
        public void HResult_MapsToTheCause(uint hresult, StorePlanProblem expected)
        {
            var code = unchecked((int)hresult);
            Assert.Equal(expected, StorePlanClassifier.Classify(code, 0, true));
            Assert.Equal($"0x{hresult:X8}", StorePlanClassifier.FormatCode(code));
        }

        [Fact]
        public void GenericStoreFailure_WhileOffline_IsNetwork()
        {
            Assert.Equal(
                StorePlanProblem.Network,
                StorePlanClassifier.Classify(StorePlanClassifier.StoreUnexpected, 0, false));
        }

        [Fact]
        public void Exceptions_UseTypeThenHResult()
        {
            Assert.Equal(StorePlanProblem.Network, StorePlanClassifier.FromException(new HttpRequestException("down")));
            Assert.Equal(
                StorePlanProblem.StoreUnavailable,
                StorePlanClassifier.FromException(new COMException("class", StorePlanClassifier.ClassNotRegistered)));
            Assert.Equal(
                StorePlanProblem.NotSignedIn,
                StorePlanClassifier.FromException(new InvalidOperationException("signed out")
                {
                    HResult = StorePlanClassifier.ErrorNoSuchUser
                }));
            Assert.Equal(StorePlanProblem.Other, StorePlanClassifier.FromException(new InvalidOperationException("?")));
        }

        [Theory]
        [InlineData(StorePlanProblem.Network, StorePlanMessages.Network)]
        [InlineData(StorePlanProblem.NotSignedIn, StorePlanMessages.NotSignedIn)]
        [InlineData(StorePlanProblem.StoreUnavailable, StorePlanMessages.StoreUnavailable)]
        [InlineData(StorePlanProblem.CatalogEmpty, StorePlanMessages.CatalogEmpty)]
        [InlineData(StorePlanProblem.Other, StorePlanMessages.Other)]
        public void Messages_MatchTheCause(StorePlanProblem problem, string expected)
        {
            Assert.Equal(expected, StorePlanMessages.For(problem));
        }

        [Fact]
        public void Messages_DoNotBlameTheNetworkForEveryFailure()
        {
            Assert.Equal("No internet connection. Reconnect, then try again.", StorePlanMessages.Network);
            Assert.DoesNotContain("internet", StorePlanMessages.NotSignedIn, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("internet", StorePlanMessages.StoreUnavailable, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("internet", StorePlanMessages.CatalogEmpty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("internet", StorePlanMessages.Other, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Swiftill Pro", StorePlanMessages.CatalogEmpty);
            Assert.Equal("Error code 0x803F6107", StorePlanMessages.ErrorCodeLine("0x803F6107"));
        }

        [Fact]
        public void SupportLink_UsesOneNumber_AndDoesNotUnlockPro()
        {
            Assert.Equal("923137643443", StoreSupportContact.WhatsAppNumber);
            var uri = StoreSupportContact.PlansDidNotLoadLink.AbsoluteUri;
            Assert.StartsWith("https://wa.me/923137643443?text=", uri);
            Assert.Contains(Uri.EscapeDataString("Swiftill Pro plans did not load"), uri);
            Assert.DoesNotContain("unlock", uri, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("pro_monthly", true, true)]
        [InlineData("pro_yearly", true, true)]
        [InlineData("PRO_Monthly", true, true)]
        [InlineData("pro_monthly", false, false)]
        [InlineData("tips_small", true, false)]
        [InlineData("prolific", true, false)]
        [InlineData(null, true, false)]
        public void OnlyProSubscriptionsArePurchasable(string? productId, bool hasSubscriptionSku, bool expected)
        {
            Assert.Equal(expected, EditionService.IsPurchasableProPlan(productId, hasSubscriptionSku));
        }

        [Fact]
        public void RequestedProductIds_AreThePublishedSubscriptions()
        {
            Assert.Equal(new[] { "pro_monthly", "pro_yearly" }, EditionService.RequestedProProductIds);
            Assert.Equal(new[] { "Durable" }, EditionService.RequestedProductKinds);
        }

        [Theory]
        [InlineData("$4.99/month", "$4.99", "month", "$4.99/month")]
        [InlineData(null, "$4.99", "month", "$4.99/month")]
        [InlineData("", "$4.99", "year", "$4.99/year")]
        [InlineData(null, "$4.99/month", "month", "$4.99/month")]
        [InlineData("  $19.99/year  ", null, null, "$19.99/year")]
        public void Price_IncludesTheBillingPeriodOnce(string? recurrence, string? price, string? period, string expected)
        {
            Assert.Equal(expected, StorePriceText.Format(recurrence, price, period));
        }
    }
}
