using POSApp.Core.Entities;

namespace POSApp.UI.Helpers
{
    public static class PermissionManager
    {
        public static bool CanAccess(User? user, string feature)
        {
            if (user == null) return false;

            return user.Role switch
            {
                "Admin" => true,
                "Manager" => feature is not ("UserManagement" or "SystemSettings" or "BackupRestore"),
                "Cashier" => feature is "Sale" or "WholeSale" or "SaleReturn" or "CustomerLedger" or "HoldSale",
                "PharmacyUser" => feature is not ("Sale" or "WholeSale" or "SaleReturn"),
                _ => false
            };
        }

        public static bool CanAccessSale(User? user)
        {
            if (user == null) return false;
            return user.Role is not "PharmacyUser";
        }

        public static bool CanViewReports(User? user)
        {
            if (user == null) return false;
            return user.Role is "Admin" or "Manager" or "PharmacyUser";
        }

        public static bool CanManageProducts(User? user)
        {
            if (user == null) return false;
            return user.Role is "Admin" or "Manager" or "PharmacyUser";
        }

        public static bool CanManageExpenses(User? user)
        {
            if (user == null) return false;
            return user.Role is "Admin" or "Manager" or "PharmacyUser";
        }

        public static bool CanManagePurchases(User? user)
        {
            if (user == null) return false;
            return user.Role is "Admin" or "Manager" or "PharmacyUser";
        }

        public static bool CanManageSuppliers(User? user)
        {
            if (user == null) return false;
            return user.Role is "Admin" or "Manager" or "PharmacyUser";
        }

        public static bool CanAccessDailySummary(User? user)
        {
            if (user == null) return false;
            return user.Role is "Admin" or "Manager" or "PharmacyUser";
        }

        public static bool CanManagePharmacies(User? user)
        {
            if (user == null) return false;
            return user.Role is "PharmacyUser";
        }

        public static bool CanManageDoctors(User? user)
        {
            if (user == null) return false;
            return user.Role is "PharmacyUser";
        }
    }
}
