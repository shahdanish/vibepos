using System.IO;
using System.Text.Json;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// How numbers are grouped when an amount is spelled out in words on a salary slip.
    /// </summary>
    public enum NumberWordStyle
    {
        /// <summary>Thousand / Million / Billion — most of the world.</summary>
        International = 0,

        /// <summary>Thousand / Lakh / Crore — South Asia (Pakistan, India, Bangladesh, Nepal).</summary>
        SouthAsian = 1
    }

    /// <summary>
    /// Everything about a printout that changes when the software is sold into a different
    /// country: the currency it prints, what the national identity document is called, and
    /// which statutory payroll deduction the shop withholds.
    ///
    /// Defaults reproduce the original Pakistani behaviour exactly, so an existing till that
    /// upgrades sees no change until someone edits Admin → Receipt Settings.
    /// </summary>
    public sealed class RegionSettingsData
    {
        /// <summary>Symbol printed before every amount, e.g. "Rs.", "$", "AED", "£".</summary>
        public string CurrencySymbol { get; set; } = "Rs.";

        /// <summary>Currency spelled out for the "amount in words" line, e.g. "Rupees", "Dollars".</summary>
        public string CurrencyName { get; set; } = "Rupees";

        /// <summary>What the identity document is called, e.g. "CNIC", "National ID", "SSN".</summary>
        public string NationalIdLabel { get; set; } = "CNIC";

        /// <summary>
        /// The statutory payroll deduction withheld in this country, e.g. "EOBI" (Pakistan),
        /// "Social Security", "NI". Printed as a salary-slip deduction row.
        /// </summary>
        public string StatutoryDeductionLabel { get; set; } = "EOBI";

        /// <summary>Lakh/Crore vs Million/Billion when spelling an amount out in words.</summary>
        public NumberWordStyle NumberWords { get; set; } = NumberWordStyle.SouthAsian;
    }

    /// <summary>
    /// Loads/saves <see cref="RegionSettingsData"/> and formats money for the whole app.
    ///
    /// Every amount the user sees goes through <see cref="Money"/> (code) or
    /// <c>MoneyConverter</c> / the <c>x:Static</c> label properties below (XAML) — nothing
    /// hardcodes a currency symbol any more. The settings file sits next to the receipt
    /// branding under %ProgramData% so every cashier on the till shares it.
    /// </summary>
    public static class Region
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ShahJeePOS",
            "region-settings.json");

        private static RegionSettingsData? _cached;

        /// <summary>Current region settings, loaded from disk on first use and cached thereafter.</summary>
        public static RegionSettingsData Current
        {
            get
            {
                if (_cached != null)
                    return _cached;

                try
                {
                    _cached = File.Exists(SettingsFilePath)
                        ? JsonSerializer.Deserialize<RegionSettingsData>(File.ReadAllText(SettingsFilePath))
                          ?? new RegionSettingsData()
                        : new RegionSettingsData();
                }
                catch
                {
                    // Missing or hand-edited file: fall back to defaults rather than
                    // blocking a sale.
                    _cached = new RegionSettingsData();
                }

                return _cached;
            }
        }

        /// <summary>Persists the settings and refreshes the cache. Returns false if the file could not be written.</summary>
        public static bool Save(RegionSettingsData settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsFilePath)!;
                Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);

                _cached = settings;
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── Formatting ────────────────────────────────────────────────────────

        /// <summary>The configured currency symbol on its own, e.g. "Rs." or "$".</summary>
        public static string Symbol => Current.CurrencySymbol;

        /// <summary>"Rs. 1,250.00" — the standard two-decimal form used almost everywhere.</summary>
        public static string Money(decimal amount) => $"{Current.CurrencySymbol} {amount:N2}";

        /// <summary>"Rs. 1,250" — no decimals, for the big highlight figure on a salary slip.</summary>
        public static string MoneyWhole(decimal amount) => $"{Current.CurrencySymbol} {amount:N0}";

        /// <summary>"+Rs. 50.00" / "-Rs. 50.00" — for a cash-drawer over/short difference.</summary>
        public static string MoneySigned(decimal amount) =>
            (amount >= 0 ? "+" : "-") + Money(Math.Abs(amount));

        // ── Labels usable from XAML via x:Static ──────────────────────────────

        /// <summary>Column/field header for a money column, e.g. "Amount (Rs.)".</summary>
        public static string AmountLabel => $"Amount ({Symbol})";

        /// <summary>Required-field variant, e.g. "Amount (Rs.) *".</summary>
        public static string AmountLabelRequired => $"{AmountLabel} *";

        /// <summary>e.g. "Balance (Rs.)".</summary>
        public static string BalanceLabel => $"Balance ({Symbol})";

        /// <summary>e.g. "Total (Rs.)".</summary>
        public static string TotalLabel => $"Total ({Symbol})";

        /// <summary>e.g. "Cost Price (Rs.)".</summary>
        public static string CostPriceLabel => $"Cost Price ({Symbol})";

        /// <summary>e.g. "New Cost Price (Rs.)".</summary>
        public static string NewCostPriceLabel => $"New Cost Price ({Symbol})";

        /// <summary>e.g. "Retail Price (Rs.)".</summary>
        public static string RetailPriceLabel => $"Retail Price ({Symbol})";

        /// <summary>e.g. "Sale Price (Rs.)".</summary>
        public static string SalePriceLabel => $"Sale Price ({Symbol})";

        /// <summary>e.g. "Wholesale Price (Rs.)".</summary>
        public static string WholesalePriceLabel => $"Wholesale Price ({Symbol})";

        /// <summary>e.g. "Initial Balance (Rs.)".</summary>
        public static string InitialBalanceLabel => $"Initial Balance ({Symbol})";

        /// <summary>e.g. "Outstanding Balance: Rs. " — the run printed before a bound amount.</summary>
        public static string OutstandingBalancePrefix => $"Outstanding Balance: {Symbol} ";

        /// <summary>e.g. "Outstanding: Rs. ".</summary>
        public static string OutstandingPrefix => $"Outstanding: {Symbol} ";

        /// <summary>e.g. "Total Bill: Rs. ".</summary>
        public static string TotalBillPrefix => $"Total Bill: {Symbol} ";

        /// <summary>e.g. "Basic Salary (Rs.)".</summary>
        public static string BasicSalaryLabel => $"Basic Salary ({Symbol})";

        /// <summary>e.g. "House Rent Allow. (Rs.)".</summary>
        public static string HouseRentLabel => $"House Rent Allow. ({Symbol})";

        /// <summary>e.g. "Medical Allow. (Rs.)".</summary>
        public static string MedicalAllowanceLabel => $"Medical Allow. ({Symbol})";

        /// <summary>e.g. "Other Allow. (Rs.)".</summary>
        public static string OtherAllowanceLabel => $"Other Allow. ({Symbol})";

        /// <summary>e.g. "Income Tax (Rs.)".</summary>
        public static string IncomeTaxLabel => $"Income Tax ({Symbol})";

        /// <summary>e.g. "Other Deductions (Rs.)".</summary>
        public static string OtherDeductionsLabel => $"Other Deductions ({Symbol})";

        /// <summary>e.g. "Opening Cash (Rs.)".</summary>
        public static string OpeningCashLabel => $"Opening Cash ({Symbol})";

        /// <summary>e.g. "Actual Cash in Drawer (Rs.)".</summary>
        public static string ActualCashLabel => $"Actual Cash in Drawer ({Symbol})";

        /// <summary>" × Rs." — sits between quantity and unit price on a report row.</summary>
        public static string TimesSymbol => $" × {Symbol}";

        /// <summary>" = Rs." — sits before a line total on a report row.</summary>
        public static string EqualsSymbol => $" = {Symbol}";

        /// <summary>The configured identity-document name, e.g. "CNIC" or "National ID".</summary>
        public static string NationalIdLabel => Current.NationalIdLabel;

        /// <summary>Identity document with a colon, for a printed key/value row.</summary>
        public static string NationalIdLabelColon => $"{NationalIdLabel}:";

        /// <summary>Placeholder text for the employee search box.</summary>
        public static string EmployeeSearchHint => $"🔍  Search by name, {NationalIdLabel}, cell, designation…";

        /// <summary>The configured statutory deduction name, e.g. "EOBI" or "Social Security".</summary>
        public static string StatutoryDeductionLabel => Current.StatutoryDeductionLabel;

        /// <summary>e.g. "EOBI (Rs.)".</summary>
        public static string StatutoryDeductionAmountLabel => $"{StatutoryDeductionLabel} ({Symbol})";

        // ── Number to words ───────────────────────────────────────────────────

        /// <summary>
        /// Spells an amount out in words for the salary slip, e.g. "One Lakh Twenty Thousand
        /// Rupees Only" or "One Hundred Twenty Thousand Dollars Only" depending on
        /// <see cref="RegionSettingsData.NumberWords"/> and the configured currency name.
        /// </summary>
        public static string AmountInWords(decimal amount)
        {
            var n = (long)Math.Floor(Math.Abs(amount));
            var name = string.IsNullOrWhiteSpace(Current.CurrencyName) ? "" : " " + Current.CurrencyName.Trim();
            return $"{ToWords(n)}{name} Only";
        }

        private static readonly string[] _ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
        private static readonly string[] _tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

        private static string ToWords(long n) =>
            Current.NumberWords == NumberWordStyle.SouthAsian ? SouthAsianWords(n) : InternationalWords(n);

        /// <summary>Thousand / Lakh / Crore grouping.</summary>
        private static string SouthAsianWords(long n)
        {
            if (n == 0)         return "Zero";
            if (n < 20)         return _ones[n];
            if (n < 100)        return _tens[n / 10] + (n % 10 > 0 ? " " + SouthAsianWords(n % 10) : "");
            if (n < 1_000)      return _ones[n / 100] + " Hundred" + (n % 100 > 0 ? " " + SouthAsianWords(n % 100) : "");
            if (n < 100_000)    return SouthAsianWords(n / 1_000) + " Thousand" + (n % 1_000 > 0 ? " " + SouthAsianWords(n % 1_000) : "");
            if (n < 10_000_000) return SouthAsianWords(n / 100_000) + " Lakh" + (n % 100_000 > 0 ? " " + SouthAsianWords(n % 100_000) : "");
            return SouthAsianWords(n / 10_000_000) + " Crore" + (n % 10_000_000 > 0 ? " " + SouthAsianWords(n % 10_000_000) : "");
        }

        /// <summary>Thousand / Million / Billion grouping.</summary>
        private static string InternationalWords(long n)
        {
            if (n == 0)                return "Zero";
            if (n < 20)                return _ones[n];
            if (n < 100)               return _tens[n / 10] + (n % 10 > 0 ? " " + InternationalWords(n % 10) : "");
            if (n < 1_000)             return _ones[n / 100] + " Hundred" + (n % 100 > 0 ? " " + InternationalWords(n % 100) : "");
            if (n < 1_000_000)         return InternationalWords(n / 1_000) + " Thousand" + (n % 1_000 > 0 ? " " + InternationalWords(n % 1_000) : "");
            if (n < 1_000_000_000)     return InternationalWords(n / 1_000_000) + " Million" + (n % 1_000_000 > 0 ? " " + InternationalWords(n % 1_000_000) : "");
            if (n < 1_000_000_000_000) return InternationalWords(n / 1_000_000_000) + " Billion" + (n % 1_000_000_000 > 0 ? " " + InternationalWords(n % 1_000_000_000) : "");
            return InternationalWords(n / 1_000_000_000_000) + " Trillion" + (n % 1_000_000_000_000 > 0 ? " " + InternationalWords(n % 1_000_000_000_000) : "");
        }
    }
}
