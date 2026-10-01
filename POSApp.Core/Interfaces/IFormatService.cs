using POSApp.Core.Services;

namespace POSApp.Core.Interfaces
{
    /// <summary>
    /// The kinds of date the app prints or shows. Each kind has one pattern per
    /// <see cref="DateStyle"/>; the day-first pattern is exactly what the app printed before
    /// dates were made regional, so Pakistani output does not change.
    /// </summary>
    public enum DateFormat
    {
        /// <summary>31/12/2026 · 12/31/2026 · 2026-12-31</summary>
        Numeric,

        /// <summary>31/12 · 12/31 · 12-31</summary>
        DayMonth,

        /// <summary>31 Dec 2026 · Dec 31, 2026 · 2026-Dec-31</summary>
        Long,

        /// <summary>The date on printed bills and ledgers: 31-Dec-2026 · 12/31/2026 · 2026-12-31</summary>
        Document,

        /// <summary>31 Dec · Dec 31 · Dec 31</summary>
        DayMonthName,

        /// <summary>Compact day and month on dialogs: 31-Dec · 12/31 · 12-31</summary>
        DocumentDayMonth,

        /// <summary>Thursday, 31 Dec 2026 · Thursday, Dec 31, 2026 · Thursday, 2026-12-31</summary>
        Weekday,

        /// <summary>Thursday, 31 December 2026 · Thursday, December 31, 2026 · Thursday, 2026-12-31</summary>
        WeekdayLong,

        /// <summary>12/2026 · 12/2026 · 2026-12</summary>
        MonthYear
    }

    /// <summary>
    /// The one place money, numbers, dates, times and phone numbers are turned into text,
    /// following the shop's <see cref="RegionSettingsData"/>. Pure: no I/O, no machine culture.
    /// </summary>
    public interface IFormatService
    {
        /// <summary>The settings in force right now.</summary>
        RegionSettingsData Settings { get; }

        /// <summary>The configured currency symbol on its own, e.g. "Rs." or "$".</summary>
        string CurrencySymbol { get; }

        /// <summary>The ISO 4217 currency code, e.g. "PKR" or "USD".</summary>
        string CurrencyCode { get; }

        /// <summary>True when US country rules apply (sales tax, US receipt).</summary>
        bool IsUnitedStates { get; }

        /// <summary>Just the digits, with the configured separators and decimal places.</summary>
        string Number(decimal amount, int? decimals = null);

        /// <summary>Digits for a narrow price column or tally: <see cref="RegionSettingsData.CompactDecimalPlaces"/>.</summary>
        string Compact(decimal amount);

        /// <summary>
        /// A price in a product list. Whole-number shops keep the original "0.##" look ("23",
        /// "23.5"); shops that print cents get full amounts ("23.00", "4.50").
        /// </summary>
        string ListPrice(decimal amount);

        /// <summary>"Rs. 1,250.00" / "$1,250.00".</summary>
        string Money(decimal amount);

        /// <summary>"Rs. 1,250" — no decimals.</summary>
        string MoneyWhole(decimal amount);

        /// <summary>"+Rs. 50.00" / "-$50.00" — for a cash-drawer over/short difference.</summary>
        string MoneySigned(decimal amount);

        /// <summary>The .NET pattern behind a <see cref="DateFormat"/> for the configured date style.</summary>
        string DatePattern(DateFormat format);

        string Date(DateTime value, DateFormat format = DateFormat.Numeric);

        /// <summary>The .NET pattern for a time of day, e.g. "hh:mm tt" or "h:mm tt".</summary>
        string TimePattern { get; }

        /// <summary>Time of day in the configured style, e.g. "02:35 PM", "2:35 PM" or "14:35".</summary>
        string Time(DateTime value);

        /// <summary>A date followed by the time of day.</summary>
        string DateAndTime(DateTime value, DateFormat format = DateFormat.Numeric);

        /// <summary>
        /// A phone number for display. US: "(555) 555-0123" when the input holds a 10-digit
        /// (or 1 + 10-digit) number; anything else, and every other region, is shown as typed.
        /// </summary>
        string Phone(string? raw);

        /// <summary>"One Lakh Twenty Thousand Rupees Only" / "One Hundred Twenty Thousand Dollars Only".</summary>
        string AmountInWords(decimal amount);
    }
}
