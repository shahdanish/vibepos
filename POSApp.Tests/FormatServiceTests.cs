using System.Globalization;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;

namespace POSApp.Tests
{
    /// <summary>
    /// The formatter is pure (no machine culture, no files), so these run in parallel.
    /// The Pakistan cases compare against the exact format strings the app used before
    /// formatting was centralised: under default settings nothing an existing till shows changes.
    /// </summary>
    public sealed class FormatServiceTests
    {
        private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
        private static readonly FormatService Pk = new(RegionSettingsData.Pakistan());
        private static readonly FormatService Us = new(RegionSettingsData.UnitedStates());

        public static TheoryData<DateTime> SampleDates => new()
        {
            new DateTime(2026, 1, 5, 0, 0, 0),      // single-digit day and month, midnight
            new DateTime(2026, 10, 14, 14, 35, 0),  // afternoon
            new DateTime(2024, 2, 29, 9, 5, 0),     // leap day, morning
            new DateTime(2026, 12, 31, 23, 59, 59)
        };

        // ── Pakistan: identical to the legacy literals ─────────────────────

        [Theory]
        [MemberData(nameof(SampleDates))]
        public void Pakistan_DatesMatchTheLegacyPatterns(DateTime d)
        {
            Assert.Equal(d.ToString("dd/MM/yyyy", EnUs), Pk.Date(d));
            Assert.Equal(d.ToString("dd/MM", EnUs), Pk.Date(d, DateFormat.DayMonth));
            Assert.Equal(d.ToString("dd MMM yyyy", EnUs), Pk.Date(d, DateFormat.Long));
            Assert.Equal(d.ToString("dd-MMM-yyyy", EnUs), Pk.Date(d, DateFormat.Document));
            Assert.Equal(d.ToString("dd MMM", EnUs), Pk.Date(d, DateFormat.DayMonthName));
            Assert.Equal(d.ToString("dd-MMM", EnUs), Pk.Date(d, DateFormat.DocumentDayMonth));
            Assert.Equal(d.ToString("dddd, dd MMM yyyy", EnUs), Pk.Date(d, DateFormat.Weekday));
            Assert.Equal(d.ToString("dddd, dd MMMM yyyy", EnUs), Pk.Date(d, DateFormat.WeekdayLong));
            Assert.Equal(d.ToString("MM/yyyy", EnUs), Pk.Date(d, DateFormat.MonthYear));
            Assert.Equal(d.ToString("hh:mm tt", EnUs), Pk.Time(d));
            Assert.Equal(d.ToString("dd-MMM-yyyy hh:mm tt", EnUs), Pk.DateAndTime(d, DateFormat.Document));
            Assert.Equal(d.ToString("dd/MM/yyyy hh:mm tt", EnUs), Pk.DateAndTime(d));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("0.005")]
        [InlineData("1.5")]
        [InlineData("23")]
        [InlineData("1250.5")]
        [InlineData("1234567.891")]
        [InlineData("-5.25")]
        [InlineData("-0.004")]
        public void Pakistan_NumbersMatchTheLegacyFormats(string value)
        {
            var x = decimal.Parse(value, CultureInfo.InvariantCulture);
            Assert.Equal(x.ToString("N2", EnUs), Pk.Number(x));
            Assert.Equal(x.ToString("N0", EnUs), Pk.Compact(x));
            Assert.Equal(x.ToString("0.##", EnUs), Pk.ListPrice(x));
            Assert.Equal("Rs. " + x.ToString("N2", EnUs), Pk.Money(x));
            Assert.Equal("Rs. " + x.ToString("N0", EnUs), Pk.MoneyWhole(x));
        }

        [Fact]
        public void Pakistan_IsTheDefaultAndHasNoUsRules()
        {
            var defaults = new RegionSettingsData();
            Assert.Equal(RegionCodes.Pakistan, defaults.RegionCode);
            Assert.False(Pk.IsUnitedStates);
            Assert.Equal("0332-3324911", Pk.Phone(" 0332-3324911 "));
        }

        [Fact]
        public void SettingsFileFromAnOlderBuild_LoadsAsPakistan()
        {
            // Exactly what the previous build wrote: no RegionCode, Culture, SymbolSpacing,
            // CompactDecimalPlaces or Times properties.
            const string legacyJson = """
                {
                  "CurrencySymbol": "Rs.",
                  "CurrencyName": "Rupees",
                  "NationalIdLabel": "CNIC",
                  "StatutoryDeductionLabel": "EOBI",
                  "NumberWords": 1,
                  "SymbolSide": 0,
                  "DecimalPlaces": 2,
                  "NumberFormat": 0,
                  "Dates": 0
                }
                """;

            var s = RegionSettingsStore.Deserialize(legacyJson);

            Assert.Equal(RegionCodes.Pakistan, s.RegionCode);
            Assert.Equal("en-PK", s.Culture);
            Assert.True(s.SymbolSpacing);
            Assert.Equal(0, s.CompactDecimalPlaces);
            Assert.Equal(TimeStyle.TwelveHourPadded, s.Times);
            Assert.Equal("PKR", s.CurrencyCode);
            Assert.Equal("Rs. 1,250.00", new FormatService(s).Money(1250m));
        }

        // ── United States ──────────────────────────────────────────────────

        [Fact]
        public void UnitedStates_Money()
        {
            Assert.True(Us.IsUnitedStates);
            Assert.Equal("$1,250.00", Us.Money(1250m));
            Assert.Equal("$3.49", Us.Money(3.49m));
            Assert.Equal("-$5.00", Us.Money(-5m));
            Assert.Equal("$0.00", Us.Money(-0.004m));   // rounds to zero: no minus sign
            Assert.Equal("$0.01", Us.Money(0.005m));    // half a cent rounds away from zero
            Assert.Equal("-$0.01", Us.Money(-0.005m));
            Assert.Equal("$1,250", Us.MoneyWhole(1250.49m));
            Assert.Equal("+$50.00", Us.MoneySigned(50m));
            Assert.Equal("-$50.00", Us.MoneySigned(-50m));
        }

        [Fact]
        public void UnitedStates_ReceiptColumnsKeepCents()
        {
            Assert.Equal("3.49", Us.Compact(3.49m));
            Assert.Equal("1,250.50", Us.Compact(1250.5m));
            Assert.Equal("4.50", Us.ListPrice(4.5m));
        }

        [Fact]
        public void UnitedStates_Dates()
        {
            var d = new DateTime(2026, 10, 14, 14, 35, 0);
            Assert.Equal("10/14/2026", Us.Date(d));
            Assert.Equal("10/14/2026", Us.Date(d, DateFormat.Document));
            Assert.Equal("Oct 14, 2026", Us.Date(d, DateFormat.Long));
            Assert.Equal("10/14", Us.Date(d, DateFormat.DayMonth));
            Assert.Equal("Oct 14", Us.Date(d, DateFormat.DayMonthName));
            Assert.Equal("Wednesday, Oct 14, 2026", Us.Date(d, DateFormat.Weekday));
            Assert.Equal("Wednesday, October 14, 2026", Us.Date(d, DateFormat.WeekdayLong));
            Assert.Equal("2:35 PM", Us.Time(d));
            Assert.Equal("10/14", Us.Date(d, DateFormat.DocumentDayMonth));
            Assert.Equal("USD", Us.CurrencyCode);
            Assert.Equal("10/14/2026 2:35 PM", Us.DateAndTime(d, DateFormat.Document));
            Assert.Equal("01/05/2026", Us.Date(new DateTime(2026, 1, 5)));
        }

        [Theory]
        [InlineData("5555550123", "(555) 555-0123")]
        [InlineData("555-555-0123", "(555) 555-0123")]
        [InlineData("(555) 555 0123", "(555) 555-0123")]
        [InlineData("+1 555 555 0123", "(555) 555-0123")]
        [InlineData("1-555-555-0123", "(555) 555-0123")]
        [InlineData("555-0142", "555-0142")]                       // 7 digits: as typed
        [InlineData("555 555 0123 x12", "555 555 0123 x12")]       // extension: as typed
        [InlineData("+44 20 7946 0958", "+44 20 7946 0958")]       // international: as typed
        [InlineData("", "")]
        [InlineData(null, "")]
        public void UnitedStates_Phone(string? raw, string expected) =>
            Assert.Equal(expected, Us.Phone(raw));

        [Fact]
        public void TimeStyles()
        {
            var d = new DateTime(2026, 10, 14, 9, 5, 0);
            Assert.Equal("09:05 AM", new FormatService(new RegionSettingsData { Times = TimeStyle.TwelveHourPadded }).Time(d));
            Assert.Equal("9:05 AM", new FormatService(new RegionSettingsData { Times = TimeStyle.TwelveHour }).Time(d));
            Assert.Equal("09:05", new FormatService(new RegionSettingsData { Times = TimeStyle.TwentyFourHour }).Time(d));
        }

        [Fact]
        public void RegionCodeComparisonIgnoresCase()
        {
            Assert.True(RegionCodes.IsUnitedStates("us"));
            Assert.NotNull(RegionSettingsData.PresetFor("us"));
            Assert.Null(RegionSettingsData.PresetFor(RegionCodes.Other));
        }

        [Fact]
        public void UsPreset_SurvivesASaveAndLoad()
        {
            var json = RegionSettingsStore.Serialize(RegionSettingsData.UnitedStates());
            var back = new FormatService(RegionSettingsStore.Deserialize(json));
            Assert.True(back.IsUnitedStates);
            Assert.Equal("$1,250.00", back.Money(1250m));
            Assert.Equal("2:35 PM", back.Time(new DateTime(2026, 10, 14, 14, 35, 0)));
        }

        [Fact]
        public void FormatterFollowsSettingsChangesImmediately()
        {
            var current = RegionSettingsData.Pakistan();
            var f = new FormatService(() => current);
            Assert.Equal("Rs. 10.00", f.Money(10m));
            current = RegionSettingsData.UnitedStates();
            Assert.Equal("$10.00", f.Money(10m));
        }
    }
}
