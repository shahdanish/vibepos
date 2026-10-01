using System.Globalization;
using POSApp.Core.Interfaces;

namespace POSApp.Core.Services
{
    /// <summary>
    /// Formats money, numbers, dates, times and phone numbers from <see cref="RegionSettingsData"/>.
    ///
    /// Month and weekday names and the AM/PM marker always come from the invariant (English)
    /// culture, never the PC's culture, so the same settings print the same receipt on any
    /// machine. Separators and patterns come from the settings alone.
    /// </summary>
    public sealed class FormatService : IFormatService
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private readonly Func<RegionSettingsData> _settings;

        /// <param name="settings">Read on every call, so a settings change applies immediately.</param>
        public FormatService(Func<RegionSettingsData> settings) => _settings = settings;

        /// <summary>A formatter over fixed settings — for previews and tests.</summary>
        public FormatService(RegionSettingsData settings) : this(() => settings) { }

        public RegionSettingsData Settings => _settings();

        public string CurrencySymbol => Settings.CurrencySymbol;

        public bool IsUnitedStates => RegionCodes.IsUnitedStates(Settings.RegionCode);

        // ── Numbers & money ───────────────────────────────────────────────

        public string Number(decimal amount, int? decimals = null)
        {
            var s = Settings;
            var d = Math.Clamp(decimals ?? s.DecimalPlaces, 0, 6);
            return amount.ToString("N" + d, NumberFormatFor(s.NumberFormat));
        }

        public string Compact(decimal amount) => Number(amount, Settings.CompactDecimalPlaces);

        public string ListPrice(decimal amount) =>
            Settings.CompactDecimalPlaces == 0
                ? amount.ToString("0.##", Invariant)
                : Number(amount);

        public string Money(decimal amount) => WithSymbol(amount, Settings.DecimalPlaces);

        public string MoneyWhole(decimal amount) => WithSymbol(amount, 0);

        public string MoneySigned(decimal amount) =>
            (amount >= 0 ? "+" : "-") + Money(Math.Abs(amount));

        private string WithSymbol(decimal amount, int decimals)
        {
            var s = Settings;

            if (s.SymbolSpacing)
            {
                // Original layout, unchanged: "Rs. 1,250.00", "Rs. -5.00", "1 250,00 kr".
                var number = Number(amount, decimals);
                return s.SymbolSide == SymbolPosition.After
                    ? $"{number} {s.CurrencySymbol}"
                    : $"{s.CurrencySymbol} {number}";
            }

            // Tight layout puts the sign outside the symbol: "$1,250.00", "-$5.00", "5,00€".
            var d = Math.Clamp(decimals, 0, 6);
            var negative = Math.Round(amount, d, MidpointRounding.AwayFromZero) < 0;
            var digits = Number(Math.Abs(amount), d);
            var sign = negative ? "-" : string.Empty;
            return s.SymbolSide == SymbolPosition.After
                ? $"{sign}{digits}{s.CurrencySymbol}"
                : $"{sign}{s.CurrencySymbol}{digits}";
        }

        private static NumberFormatInfo NumberFormatFor(NumberFormatStyle style)
        {
            var nfi = (NumberFormatInfo)Invariant.NumberFormat.Clone();
            (nfi.NumberGroupSeparator, nfi.NumberDecimalSeparator) = style switch
            {
                NumberFormatStyle.CommaDecimal => (".", ","),
                NumberFormatStyle.SpaceGroupDotDecimal => (" ", "."),
                NumberFormatStyle.SpaceGroupCommaDecimal => (" ", ","),
                _ => (",", ".")
            };
            return nfi;
        }

        // ── Dates & times ─────────────────────────────────────────────────

        public string DatePattern(DateFormat format)
        {
            var style = Settings.Dates;
            return format switch
            {
                DateFormat.Numeric => Pick(style, "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd"),
                DateFormat.DayMonth => Pick(style, "dd/MM", "MM/dd", "MM-dd"),
                DateFormat.Long => Pick(style, "dd MMM yyyy", "MMM dd, yyyy", "yyyy-MMM-dd"),
                DateFormat.Document => Pick(style, "dd-MMM-yyyy", "MM/dd/yyyy", "yyyy-MM-dd"),
                DateFormat.DayMonthName => Pick(style, "dd MMM", "MMM dd", "MMM dd"),
                DateFormat.Weekday => Pick(style, "dddd, dd MMM yyyy", "dddd, MMM dd, yyyy", "dddd, yyyy-MM-dd"),
                DateFormat.WeekdayLong => Pick(style, "dddd, dd MMMM yyyy", "dddd, MMMM dd, yyyy", "dddd, yyyy-MM-dd"),
                DateFormat.MonthYear => Pick(style, "MM/yyyy", "MM/yyyy", "yyyy-MM"),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
            };
        }

        private static string Pick(DateStyle style, string dayFirst, string monthFirst, string iso) => style switch
        {
            DateStyle.MonthFirst => monthFirst,
            DateStyle.Iso => iso,
            _ => dayFirst
        };

        public string Date(DateTime value, DateFormat format = DateFormat.Numeric) =>
            value.ToString(DatePattern(format), Invariant);

        public string TimePattern => Settings.Times switch
        {
            TimeStyle.TwelveHour => "h:mm tt",
            TimeStyle.TwentyFourHour => "HH:mm",
            _ => "hh:mm tt"
        };

        public string Time(DateTime value) => value.ToString(TimePattern, Invariant);

        public string DateAndTime(DateTime value, DateFormat format = DateFormat.Numeric) =>
            $"{Date(value, format)} {Time(value)}";

        // ── Phone numbers ─────────────────────────────────────────────────

        public string Phone(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var typed = raw.Trim();
            if (!IsUnitedStates) return typed;

            // Only reformat a plain North American number; extensions, short codes and
            // international numbers are shown exactly as the cashier typed them.
            if (typed.Any(char.IsLetter)) return typed;
            var digits = new string(typed.Where(char.IsDigit).ToArray());
            if (digits.Length == 11 && digits[0] == '1') digits = digits[1..];

            return digits.Length == 10
                ? $"({digits[..3]}) {digits[3..6]}-{digits[6..]}"
                : typed;
        }

        // ── Number to words ───────────────────────────────────────────────

        public string AmountInWords(decimal amount)
        {
            var s = Settings;
            var n = (long)Math.Floor(Math.Abs(amount));
            var name = string.IsNullOrWhiteSpace(s.CurrencyName) ? "" : " " + s.CurrencyName.Trim();
            var words = s.NumberWords == NumberWordStyle.SouthAsian ? SouthAsianWords(n) : InternationalWords(n);
            return $"{words}{name} Only";
        }

        private static readonly string[] Ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
        private static readonly string[] Tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

        /// <summary>Thousand / Lakh / Crore grouping.</summary>
        private static string SouthAsianWords(long n)
        {
            if (n == 0)         return "Zero";
            if (n < 20)         return Ones[n];
            if (n < 100)        return Tens[n / 10] + (n % 10 > 0 ? " " + SouthAsianWords(n % 10) : "");
            if (n < 1_000)      return Ones[n / 100] + " Hundred" + (n % 100 > 0 ? " " + SouthAsianWords(n % 100) : "");
            if (n < 100_000)    return SouthAsianWords(n / 1_000) + " Thousand" + (n % 1_000 > 0 ? " " + SouthAsianWords(n % 1_000) : "");
            if (n < 10_000_000) return SouthAsianWords(n / 100_000) + " Lakh" + (n % 100_000 > 0 ? " " + SouthAsianWords(n % 100_000) : "");
            return SouthAsianWords(n / 10_000_000) + " Crore" + (n % 10_000_000 > 0 ? " " + SouthAsianWords(n % 10_000_000) : "");
        }

        /// <summary>Thousand / Million / Billion grouping.</summary>
        private static string InternationalWords(long n)
        {
            if (n == 0)                return "Zero";
            if (n < 20)                return Ones[n];
            if (n < 100)               return Tens[n / 10] + (n % 10 > 0 ? " " + InternationalWords(n % 10) : "");
            if (n < 1_000)             return Ones[n / 100] + " Hundred" + (n % 100 > 0 ? " " + InternationalWords(n % 100) : "");
            if (n < 1_000_000)         return InternationalWords(n / 1_000) + " Thousand" + (n % 1_000 > 0 ? " " + InternationalWords(n % 1_000) : "");
            if (n < 1_000_000_000)     return InternationalWords(n / 1_000_000) + " Million" + (n % 1_000_000 > 0 ? " " + InternationalWords(n % 1_000_000) : "");
            if (n < 1_000_000_000_000) return InternationalWords(n / 1_000_000_000) + " Billion" + (n % 1_000_000_000 > 0 ? " " + InternationalWords(n % 1_000_000_000) : "");
            return InternationalWords(n / 1_000_000_000_000) + " Trillion" + (n % 1_000_000_000_000 > 0 ? " " + InternationalWords(n % 1_000_000_000_000) : "");
        }
    }
}
