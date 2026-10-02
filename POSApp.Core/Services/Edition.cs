namespace POSApp.Core.Services
{
    /// <summary>Which product the running copy is.</summary>
    public enum AppEdition
    {
        /// <summary>Classic install sold directly to shops (Inno Setup). Every feature, yearly licence.</summary>
        Direct,

        /// <summary>Microsoft Store build, free tier.</summary>
        StoreLite,

        /// <summary>Microsoft Store build with the "Pro" add-on purchased.</summary>
        StorePro
    }

    /// <summary>Features that differ between editions. Anything not listed is available everywhere.</summary>
    public enum AppFeature
    {
        Wholesale,
        Purchases,          // purchase entry/returns, suppliers, demand orders
        Expenses,
        ExcelExport,
        MultiUser,          // more than LiteMaxUsers active users
        RoleManagement,
        HumanResources,     // employees + salary slips
        Pharmacy,           // pharmacy sale, pharmacies, doctors, reps, call schedule
        CloudBackup,
        YearlyLicence,      // the offline renewal-code licence gate
        FrontStorePharmacy  // US pharmacy front store: PSE logbook, FSA/HSA flags, expiry report
    }

    /// <summary>
    /// The edition/feature matrix — pure logic, no I/O, so it can be unit tested.
    /// </summary>
    public static class EditionPolicy
    {
        /// <summary>Active users allowed in the free Store tier (an owner plus one cashier).</summary>
        public const int LiteMaxUsers = 2;

        /// <summary>Features that are part of the paid "Pro" add-on in the Store build.</summary>
        public static readonly IReadOnlyList<AppFeature> ProFeatures = new[]
        {
            AppFeature.Wholesale,
            AppFeature.Purchases,
            AppFeature.Expenses,
            AppFeature.ExcelExport,
            AppFeature.MultiUser,
            AppFeature.RoleManagement,
            AppFeature.HumanResources,
            AppFeature.FrontStorePharmacy
        };

        /// <summary>True when the build contains the feature at all (whether or not it is unlocked).</summary>
        public static bool IsInBuild(AppEdition edition, AppFeature feature) => feature switch
        {
            // Never shipped in the Store: pharmacy is a vertical with regulatory questions,
            // cloud backup needs a service-account key that must not be inside a public package,
            // and licensing is the Store's job.
            AppFeature.Pharmacy or AppFeature.CloudBackup or AppFeature.YearlyLicence
                => edition == AppEdition.Direct,
            _ => true
        };

        /// <summary>True when the feature can be used right now.</summary>
        public static bool IsEnabled(AppEdition edition, AppFeature feature)
        {
            if (!IsInBuild(edition, feature)) return false;
            if (edition == AppEdition.StoreLite && ProFeatures.Contains(feature)) return false;
            return true;
        }

        /// <summary>True when the feature exists in this build but needs the Pro upgrade.</summary>
        public static bool IsUpgradeable(AppEdition edition, AppFeature feature)
            => IsInBuild(edition, feature) && !IsEnabled(edition, feature);

        /// <summary>Maximum active users, or null for unlimited.</summary>
        public static int? MaxActiveUsers(AppEdition edition)
            => IsEnabled(edition, AppFeature.MultiUser) ? null : LiteMaxUsers;

        public static bool IsStore(AppEdition edition) => edition != AppEdition.Direct;

        /// <summary>Human-readable name of a feature for upgrade prompts.</summary>
        public static string DisplayName(AppFeature feature) => feature switch
        {
            AppFeature.Wholesale      => "Wholesale mode",
            AppFeature.Purchases      => "Purchases & suppliers",
            AppFeature.Expenses       => "Expense tracking",
            AppFeature.ExcelExport    => "Excel export",
            AppFeature.MultiUser      => "More than two users",
            AppFeature.RoleManagement => "Custom roles & permissions",
            AppFeature.HumanResources => "Employees & salary slips",
            AppFeature.Pharmacy       => "Pharmacy module",
            AppFeature.CloudBackup    => "Cloud backup",
            AppFeature.YearlyLicence  => "Licence",
            AppFeature.FrontStorePharmacy => "Pharmacy front store (PSE logbook, FSA/HSA, expiry report)",
            _ => feature.ToString()
        };
    }
}
