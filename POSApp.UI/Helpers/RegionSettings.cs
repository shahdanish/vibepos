using System.Globalization;
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

    /// <summary>Which side of the amount the currency symbol sits on.</summary>
    public enum SymbolPosition
    {
        /// <summary>"Rs. 1,250.00" / "$ 1,250.00" — most currencies.</summary>
        Before = 0,

        /// <summary>"1 250,00 kr" / "1.250,00 EUR" — many European currencies.</summary>
        After = 1
    }

    /// <summary>Which separators group the digits.</summary>
    public enum NumberFormatStyle
    {
        /// <summary>1,234.56 — Pakistan, India, UK, US, most of Asia.</summary>
        DotDecimal = 0,

        /// <summary>1.234,56 — Germany, Italy, Spain, Turkey, Brazil.</summary>
        CommaDecimal = 1,

        /// <summary>1 234.56 — space grouping with a dot decimal.</summary>
        SpaceGroupDotDecimal = 2,

        /// <summary>1 234,56 — space grouping with a comma decimal (France, Scandinavia).</summary>
        SpaceGroupCommaDecimal = 3
    }

    /// <summary>How dates are written on screen and on printouts.</summary>
    public enum DateStyle
    {
        /// <summary>31/12/2026 — most of the world.</summary>
        DayFirst = 0,

        /// <summary>12/31/2026 — United States.</summary>
        MonthFirst = 1,

        /// <summary>2026-12-31 — ISO, unambiguous.</summary>
        Iso = 2
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

        /// <summary>Whether the symbol goes before or after the number.</summary>
        public SymbolPosition SymbolSide { get; set; } = SymbolPosition.Before;

        /// <summary>
        /// Decimals shown on every amount. 2 for most currencies, 0 for ones with no
        /// sub-unit in daily use (JPY, KRW, UGX), 3 for KWD / BHD / OMR.
        /// </summary>
        public int DecimalPlaces { get; set; } = 2;

        /// <summary>Thousand- and decimal-separator style.</summary>
        public NumberFormatStyle NumberFormat { get; set; } = NumberFormatStyle.DotDecimal;

        /// <summary>Day-first, month-first (US) or ISO dates.</summary>
        public DateStyle Dates { get; set; } = DateStyle.DayFirst;
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

        /// <summary>
        /// Swaps the in-memory settings without touching disk. Used by the settings screen to
        /// render a preview of unsaved values; always restore the previous settings afterwards.
        /// </summary>
        public static void Apply(RegionSettingsData settings) => _cached = settings;

        // ── Formatting ────────────────────────────────────────────────────────

        /// <summary>The configured currency symbol on its own, e.g. "Rs." or "$".</summary>
        public static string Symbol => Current.CurrencySymbol;

        /// <summary>Digit grouping / decimal separators for the configured number format.</summary>
        private static NumberFormatInfo Nfi
        {
            get
            {
                var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                switch (Current.NumberFormat)
                {
                    case NumberFormatStyle.CommaDecimal:
                        nfi.NumberGroupSeparator = ".";
                        nfi.NumberDecimalSeparator = ",";
                        break;
                    case NumberFormatStyle.SpaceGroupDotDecimal:
                        nfi.NumberGroupSeparator = "\u00a0";
                        nfi.NumberDecimalSeparator = ".";
                        break;
                    case NumberFormatStyle.SpaceGroupCommaDecimal:
                        nfi.NumberGroupSeparator = "\u00a0";
                        nfi.NumberDecimalSeparator = ",";
                        break;
                    default:
                        nfi.NumberGroupSeparator = ",";
                        nfi.NumberDecimalSeparator = ".";
                        break;
                }
                return nfi;
            }
        }

        /// <summary>Just the digits, with the configured separators and decimal places.</summary>
        public static string Number(decimal amount, int? decimals = null)
        {
            var d = Math.Clamp(decimals ?? Current.DecimalPlaces, 0, 6);
            return amount.ToString("N" + d, Nfi);
        }

        /// <summary>Puts the symbol on the configured side of an already-formatted number.</summary>
        private static string WithSymbol(string number) =>
            Current.SymbolSide == SymbolPosition.After
                ? $"{number} {Current.CurrencySymbol}"
                : $"{Current.CurrencySymbol} {number}";

        /// <summary>"Rs. 1,250.00" — the standard form used almost everywhere.</summary>
        public static string Money(decimal amount) => WithSymbol(Number(amount));

        /// <summary>"Rs. 1,250" — no decimals, for the big highlight figure on a salary slip.</summary>
        public static string MoneyWhole(decimal amount) => WithSymbol(Number(amount, 0));

        /// <summary>"+Rs. 50.00" / "-Rs. 50.00" — for a cash-drawer over/short difference.</summary>
        public static string MoneySigned(decimal amount) =>
            (amount >= 0 ? "+" : "-") + Money(Math.Abs(amount));

        // ── Dates ─────────────────────────────────────────────────────────────

        /// <summary>Numeric date pattern for the configured style, e.g. "dd/MM/yyyy".</summary>
        public static string DatePattern => Current.Dates switch
        {
            DateStyle.MonthFirst => "MM/dd/yyyy",
            DateStyle.Iso        => "yyyy-MM-dd",
            _                    => "dd/MM/yyyy"
        };

        /// <summary>Date pattern with a spelled-out month, e.g. "dd MMM yyyy".</summary>
        public static string LongDatePattern => Current.Dates switch
        {
            DateStyle.MonthFirst => "MMM dd, yyyy",
            DateStyle.Iso        => "yyyy-MMM-dd",
            _                    => "dd MMM yyyy"
        };

        /// <summary>Day and month only, e.g. "31/12".</summary>
        public static string ShortDatePattern => Current.Dates switch
        {
            DateStyle.MonthFirst => "MM/dd",
            DateStyle.Iso        => "MM-dd",
            _                    => "dd/MM"
        };

        /// <summary>Date-and-time pattern with a 12-hour clock.</summary>
        public static string DateTimePattern => DatePattern + " hh:mm tt";

        /// <summary>Numeric date in the configured order.</summary>
        public static string Date(DateTime value) =>
            value.ToString(DatePattern, CultureInfo.InvariantCulture);

        /// <summary>Date with a spelled-out month.</summary>
        public static string LongDate(DateTime value) =>
            value.ToString(LongDatePattern, CultureInfo.InvariantCulture);

        /// <summary>Date plus a 12-hour time.</summary>
        public static string DateTimeText(DateTime value) =>
            value.ToString(DateTimePattern, CultureInfo.InvariantCulture);

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
