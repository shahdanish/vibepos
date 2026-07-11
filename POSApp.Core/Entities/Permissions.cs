namespace POSApp.Core.Entities
{
    public static class Permissions
    {
        // Sales
        public const string SaleAccess = "Sale.Access";
        public const string WholeSaleAccess = "WholeSale.Access";
        public const string SaleReturnAccess = "SaleReturn.Access";
        public const string CustomerLedgerAccess = "CustomerLedger.Access";
        public const string HoldSaleAccess = "HoldSale.Access";

        // Reports
        public const string ReportsSales = "Reports.Sales";
        public const string ReportsDaily = "Reports.Daily";
        public const string DashboardAccess = "Dashboard.Access";

        // Products
        public const string ProductsManage = "Products.Manage";
        public const string CategoriesManage = "Categories.Manage";

        // Operations
        public const string ExpensesManage = "Expenses.Manage";
        public const string ShiftsManage = "Shifts.Manage";
        public const string PurchasesManage = "Purchases.Manage";
        public const string SuppliersManage = "Suppliers.Manage";

        // Administration
        public const string UsersManage = "Users.Manage";
        public const string SystemSettings = "Settings.System";
        public const string BackupAccess = "Backup.Access";

        // Pharmacy
        public const string PharmacySale = "Pharmacy.Sale";
        public const string PharmacyManage = "Pharmacy.Manage";
        public const string DoctorsManage = "Doctors.Manage";
        public const string CallScheduleManage = "CallSchedule.Manage";

        // HR
        public const string EmployeesManage = "Employees.Manage";
        public const string SalaryManage = "Salary.Manage";
    }
}
