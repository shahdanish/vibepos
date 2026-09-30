using POSApp.Core.Services;

namespace POSApp.Tests
{
    public class EditionPolicyTests
    {
        [Fact]
        public void Direct_EnablesEveryFeature()
        {
            foreach (var feature in Enum.GetValues<AppFeature>())
                Assert.True(EditionPolicy.IsEnabled(AppEdition.Direct, feature), feature.ToString());

            Assert.Null(EditionPolicy.MaxActiveUsers(AppEdition.Direct));
        }

        [Theory]
        [InlineData(AppFeature.Pharmacy)]
        [InlineData(AppFeature.CloudBackup)]
        [InlineData(AppFeature.YearlyLicence)]
        public void StoreBuilds_NeverContainDirectOnlyFeatures(AppFeature feature)
        {
            Assert.False(EditionPolicy.IsInBuild(AppEdition.StoreLite, feature));
            Assert.False(EditionPolicy.IsInBuild(AppEdition.StorePro, feature));
            Assert.False(EditionPolicy.IsEnabled(AppEdition.StorePro, feature));
            Assert.False(EditionPolicy.IsUpgradeable(AppEdition.StoreLite, feature));
        }

        [Fact]
        public void StoreLite_LocksProFeatures_ButOffersUpgrade()
        {
            foreach (var feature in EditionPolicy.ProFeatures)
            {
                Assert.False(EditionPolicy.IsEnabled(AppEdition.StoreLite, feature), feature.ToString());
                Assert.True(EditionPolicy.IsUpgradeable(AppEdition.StoreLite, feature), feature.ToString());
            }

            Assert.Equal(EditionPolicy.LiteMaxUsers, EditionPolicy.MaxActiveUsers(AppEdition.StoreLite));
        }

        [Fact]
        public void StorePro_UnlocksProFeatures()
        {
            foreach (var feature in EditionPolicy.ProFeatures)
                Assert.True(EditionPolicy.IsEnabled(AppEdition.StorePro, feature), feature.ToString());

            Assert.Null(EditionPolicy.MaxActiveUsers(AppEdition.StorePro));
        }

        [Fact]
        public void CoreSellingFeatures_AreNotBehindTheUpgrade()
        {
            // Everything a small shop needs day to day must work in the free tier.
            Assert.DoesNotContain(AppFeature.Pharmacy, EditionPolicy.ProFeatures);
            Assert.DoesNotContain(AppFeature.CloudBackup, EditionPolicy.ProFeatures);
        }
    }
}

namespace POSApp.Tests
{
    public class ProSubscriptionIdTests
    {
        [Theory]
        [InlineData("pro_monthly", true)]
        [InlineData("pro_yearly", true)]
        [InlineData("PRO_Monthly", true)]
        [InlineData("pro_upgrade", true)]
        [InlineData("prolific", false)]
        [InlineData("tips_small", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void OnlyPrefixedAddOnsGrantPro(string? productId, bool expected)
        {
            Assert.Equal(expected, POSApp.Infrastructure.Services.EditionService.IsProProductId(productId));
        }
    }
}
