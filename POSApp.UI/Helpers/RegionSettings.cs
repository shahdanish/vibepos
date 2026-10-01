using POSApp.Core.Interfaces;
using POSApp.Core.Services;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// Static entry point to the shop's regional formatting for view-models, code-behind and
    /// XAML (<c>x:Static</c>), in the same spirit as <see cref="SessionManager"/>.
    ///
    /// The rules live in <see cref="FormatService"/> (POSApp.Core) and the settings in
    /// <see cref="RegionSettingsStore"/>; this class only forwards to them. Every amount and date
    /// the user sees goes through here, <c>MoneyConverter</c>, <c>NumberConverter</c> or
    /// <c>DateConverter</c> — nothing hardcodes a currency symbol or a date order.
    /// </summary>
    public static class Region
    {
        /// <summary>The formatter over the live settings; also registered in DI as <see cref="IFormatService"/>.</summary>
        public static IFormatService Format { get; } = new FormatService(() => RegionSettingsStore.Current);

        /// <summary>Current region settings, loaded from disk on first use and cached thereafter.</summary>
        public static RegionSettingsData Current => RegionSettingsStore.Current;

        /// <summary>Persists the settings and refreshes the cache. Returns false if the file could not be written.</summary>
        public static bool Save(RegionSettingsData settings) => RegionSettingsStore.Save(settings);

        /// <summary>
        /// Swaps the in-memory settings without touching disk. Used by the settings screen to
        /// render a preview of unsaved values; always restore the previous settings afterwards.
        /// </summary>
        public static void Apply(RegionSettingsData settings) => RegionSettingsStore.Apply(settings);

        /// <summary>True when US country rules apply.</summary>
        public static bool IsUnitedStates => Format.IsUnitedStates;

        // ── Formatting ────────────────────────────────────────────────────────

        /// <summary>The configured currency symbol on its own, e.g. "Rs." or "$".</summary>
        public static string Symbol => Format.CurrencySymbol;

        /// <summary>Just the digits, with the configured separators and decimal places.</summary>
        public static string Number(decimal amount, int? decimals = null) => Format.Number(amount, decimals);

        /// <summary>Digits for a narrow receipt price column or a compact tally.</summary>
        public static string Compact(decimal amount) => Format.Compact(amount);

        /// <summary>A price in a product list ("23" / "23.5" for whole-number shops, "4.50" otherwise).</summary>
        public static string ListPrice(decimal amount) => Format.ListPrice(amount);

        /// <summary>"Rs. 1,250.00" / "$1,250.00" — the standard form used almost everywhere.</summary>
        public static string Money(decimal amount) => Format.Money(amount);

        /// <summary>"Rs. 1,250" — no decimals, for the big highlight figure on a salary slip.</summary>
        public static string MoneyWhole(decimal amount) => Format.MoneyWhole(amount);

        /// <summary>"+Rs. 50.00" / "-$50.00" — for a cash-drawer over/short difference.</summary>
        public static string MoneySigned(decimal amount) => Format.MoneySigned(amount);

        /// <summary>A phone number for display ("(555) 555-0123" in the US, as typed elsewhere).</summary>
        public static string Phone(string? raw) => Format.Phone(raw);

        // ── Dates ─────────────────────────────────────────────────────────────

        /// <summary>Numeric date pattern for the configured style, e.g. "dd/MM/yyyy".</summary>
        public static string DatePattern => Format.DatePattern(DateFormat.Numeric);

        /// <summary>Date pattern with a spelled-out month, e.g. "dd MMM yyyy".</summary>
        public static string LongDatePattern => Format.DatePattern(DateFormat.Long);

        /// <summary>Day and month only, e.g. "31/12".</summary>
        public static string ShortDatePattern => Format.DatePattern(DateFormat.DayMonth);

        /// <summary>Date-and-time pattern in the configured date and time styles.</summary>
        public static string DateTimePattern => DatePattern + " " + Format.TimePattern;

        /// <summary>Numeric date in the configured order.</summary>
        public static string Date(DateTime value) => Format.Date(value);

        /// <summary>A date in one of the app's <see cref="DateFormat"/> kinds.</summary>
        public static string Date(DateTime value, DateFormat format) => Format.Date(value, format);

        /// <summary>Date with a spelled-out month.</summary>
        public static string LongDate(DateTime value) => Format.Date(value, DateFormat.Long);

        /// <summary>Date plus the time of day.</summary>
        public static string DateTimeText(DateTime value) => Format.DateAndTime(value);

        /// <summary>The date and time printed on bills, e.g. "14-Oct-2026 02:35 PM" / "10/14/2026 02:35 PM".</summary>
        public static string DocumentDateTime(DateTime value) => Format.DateAndTime(value, DateFormat.Document);

        /// <summary>Time of day, e.g. "02:35 PM" (default) or "2:35 PM" (US).</summary>
        public static string Time(DateTime value) => Format.Time(value);

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

        /// <summary>The ISO currency code, e.g. "PKR" or "USD".</summary>
        public static string CurrencyCode => Format.CurrencyCode;

        /// <summary>Tooltip for the flat-amount discount toggle, e.g. "Fixed amount discount (PKR)".</summary>
        public static string FixedDiscountTooltip => $"Fixed amount discount ({CurrencyCode})";

        // ── Number to words ───────────────────────────────────────────────────

        /// <summary>
        /// Spells an amount out in words for the salary slip, e.g. "One Lakh Twenty Thousand
        /// Rupees Only" or "One Hundred Twenty Thousand Dollars Only".
        /// </summary>
        public static string AmountInWords(decimal amount) => Format.AmountInWords(amount);
    }
}
