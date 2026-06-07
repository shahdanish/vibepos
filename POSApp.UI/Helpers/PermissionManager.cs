using POSApp.Core.Entities;

namespace POSApp.UI.Helpers
{
    public static class PermissionManager
    {
        public static bool CanAccess(User? user, string feature)
            => user != null && SessionManager.HasPermission(feature);

        public static bool CanAccessSale(User? user)
            => user != null && SessionManager.HasPermission(Permissions.SaleAccess);

        public static bool CanViewReports(User? user)
            => user != null && SessionManager.HasPermission(Permissions.ReportsSales);

        public static bool CanManageProducts(User? user)
            => user != null && SessionManager.HasPermission(Permissions.ProductsManage);

        public static bool CanManageExpenses(User? user)
            => user != null && SessionManager.HasPermission(Permissions.ExpensesManage);

        public static bool CanManagePurchases(User? user)
            => user != null && SessionManager.HasPermission(Permissions.PurchasesManage);

        public static bool CanManageSuppliers(User? user)
            => user != null && SessionManager.HasPermission(Permissions.SuppliersManage);

        public static bool CanAccessDailySummary(User? user)
            => user != null && SessionManager.HasPermission(Permissions.ReportsDaily);

        public static bool CanManagePharmacies(User? user)
            => user != null && SessionManager.HasPermission(Permissions.PharmacyManage);

        public static bool CanManageDoctors(User? user)
            => user != null && SessionManager.HasPermission(Permissions.DoctorsManage);
    }
}
