namespace POSApp.Core.Services
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
        /// <summary>"Rs. 1,250.00" / "$1,250.00" — most currencies.</summary>
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

    /// <summary>How times of day are written.</summary>
    public enum TimeStyle
    {
        /// <summary>02:35 PM — the original format.</summary>
        TwelveHourPadded = 0,

        /// <summary>2:35 PM — United States.</summary>
        TwelveHour = 1,

        /// <summary>14:35 — 24-hour clock.</summary>
        TwentyFourHour = 2
    }

    /// <summary>
    /// The country the shop trades in. Country rules (US sales tax, the US receipt layout)
    /// switch on this code; everything about how numbers and dates look is in the separate,
    /// individually editable fields of <see cref="RegionSettingsData"/>.
    /// </summary>
    public static class RegionCodes
    {
        public const string Pakistan = "PK";
        public const string UnitedStates = "US";

        /// <summary>Any other country: no country rules, formatting exactly as configured.</summary>
        public const string Other = "XX";

        public static bool IsUnitedStates(string? code) =>
            string.Equals(code, UnitedStates, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Everything about a printout that changes when the software is sold into a different
    /// country: the currency it prints, how numbers and dates look, what the national identity
    /// document is called, and which statutory payroll deduction the shop withholds.
    ///
    /// Defaults are the United States: a new install is a US shop until it picks another
    /// currency in first-run setup or Business Settings. Existing tills are protected two ways:
    /// a settings file written before these US defaults is read on top of the
    /// <see cref="Pakistan"/> preset (see <see cref="RegionSettingsStore.Deserialize"/>), and an
    /// existing install that never saved a file gets the Pakistan preset written for it on its
    /// first start on this version (<see cref="RegionSettingsStore.KeepLegacyDefaultsIfUnset"/>).
    /// </summary>
    public sealed class RegionSettingsData
    {
        /// <summary>Country code, see <see cref="RegionCodes"/>. Drives country rules, not formatting.</summary>
        public string RegionCode { get; set; } = RegionCodes.UnitedStates;

        /// <summary>
        /// The culture name the preset was built from, e.g. "en-PK", "en-US". Kept for reference
        /// and future translation; printed formats come from the explicit fields below so that an
        /// operating-system culture update can never change what a receipt looks like.
        /// </summary>
        public string Culture { get; set; } = "en-US";

        /// <summary>Symbol printed before every amount, e.g. "$", "Rs.", "AED", "£".</summary>
        public string CurrencySymbol { get; set; } = "$";

        /// <summary>Currency spelled out for the "amount in words" line, e.g. "Dollars", "Rupees".</summary>
        public string CurrencyName { get; set; } = "Dollars";

        /// <summary>ISO 4217 code, e.g. "USD", "PKR" — for labels where a symbol is ambiguous ("$").</summary>
        public string CurrencyCode { get; set; } = "USD";

        /// <summary>
        /// What the identity document is called, e.g. "ID Number", "CNIC". Deliberately not
        /// "SSN" for the US: the software should not invite shops to collect one.
        /// </summary>
        public string NationalIdLabel { get; set; } = "ID Number";

        /// <summary>
        /// The statutory payroll deduction withheld in this country, e.g. "Social Security",
        /// "EOBI" (Pakistan), "NI". Printed as a salary-slip deduction row.
        /// </summary>
        public string StatutoryDeductionLabel { get; set; } = "Social Security";

        /// <summary>Million/Billion vs Lakh/Crore when spelling an amount out in words.</summary>
        public NumberWordStyle NumberWords { get; set; } = NumberWordStyle.International;

        /// <summary>Whether the symbol goes before or after the number.</summary>
        public SymbolPosition SymbolSide { get; set; } = SymbolPosition.Before;

        /// <summary>
        /// Whether a space separates symbol and number: "Rs. 1,250.00" (true) or "$1,250.00"
        /// (false). Without the space a negative amount reads "-$5.00".
        /// </summary>
        public bool SymbolSpacing { get; set; } = false;

        /// <summary>
        /// Decimals shown on every amount. 2 for most currencies, 0 for ones with no
        /// sub-unit in daily use (JPY, KRW, UGX), 3 for KWD / BHD / OMR.
        /// </summary>
        public int DecimalPlaces { get; set; } = 2;

        /// <summary>
        /// Decimals in the narrow price columns of a receipt and in compact on-screen tallies.
        /// 2 is needed wherever prices carry cents ("3.49"); 0 keeps the whole-number look
        /// Pakistani tills have always printed ("23", "1,251").
        /// </summary>
        public int CompactDecimalPlaces { get; set; } = 2;

        /// <summary>Thousand- and decimal-separator style.</summary>
        public NumberFormatStyle NumberFormat { get; set; } = NumberFormatStyle.DotDecimal;

        /// <summary>Month-first (US), day-first or ISO dates.</summary>
        public DateStyle Dates { get; set; } = DateStyle.MonthFirst;

        /// <summary>12-hour (US), 12-hour with a leading zero (original Pakistani) or 24-hour times.</summary>
        public TimeStyle Times { get; set; } = TimeStyle.TwelveHour;

        /// <summary>
        /// Pakistan — the original behaviour, kept exactly for the existing Pakistani tills:
        /// "Rs. 1,250.00", 14/10/2026, 02:35 PM, Lakh/Crore in words.
        /// </summary>
        public static RegionSettingsData Pakistan() => new()
        {
            RegionCode = RegionCodes.Pakistan,
            Culture = "en-PK",
            CurrencySymbol = "Rs.",
            CurrencyName = "Rupees",
            CurrencyCode = "PKR",
            NationalIdLabel = "CNIC",
            StatutoryDeductionLabel = "EOBI",
            NumberWords = NumberWordStyle.SouthAsian,
            SymbolSide = SymbolPosition.Before,
            SymbolSpacing = true,
            DecimalPlaces = 2,
            CompactDecimalPlaces = 0,
            NumberFormat = NumberFormatStyle.DotDecimal,
            Dates = DateStyle.DayFirst,
            Times = TimeStyle.TwelveHourPadded
        };

        /// <summary>United States — the default: "$1,250.00", 10/14/2026, 2:35 PM, (555) 555-0123.</summary>
        public static RegionSettingsData UnitedStates() => new();

        /// <summary>The preset for a region code, or null when the code has no preset.</summary>
        public static RegionSettingsData? PresetFor(string? regionCode) => regionCode?.ToUpperInvariant() switch
        {
            RegionCodes.Pakistan => Pakistan(),
            RegionCodes.UnitedStates => UnitedStates(),
            _ => null
        };

        public RegionSettingsData Clone() => (RegionSettingsData)MemberwiseClone();
    }
}
