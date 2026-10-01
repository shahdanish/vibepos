using POSApp.UI.Helpers;
using Xunit;
using POSApp.Core.Services;

namespace POSApp.Tests
{
    /// <summary>
    /// Guards the currency/region layer that lets one build ship to any country.
    /// Each test sets the settings it needs and restores the original afterwards; the run
    /// itself points at a temporary data folder (see <see cref="TestEnvironment"/>).
    /// </summary>
    [Collection(GlobalStateCollection.Name)]
    public class RegionSettingsTests : IDisposable
    {
        private readonly RegionSettingsData _original = Region.Current;

        public void Dispose() => Region.Save(_original);

        private static void Use(string symbol, string name, NumberWordStyle words) =>
            Region.Save(new RegionSettingsData
            {
                CurrencySymbol = symbol,
                CurrencyName = name,
                NumberWords = words
            });

        [Fact]
        public void Money_UsesConfiguredSymbol()
        {
            Use("$", "Dollars", NumberWordStyle.International);
            Assert.Equal("$ 1,250.00", Region.Money(1250m));

            Use("Rs.", "Rupees", NumberWordStyle.SouthAsian);
            Assert.Equal("Rs. 1,250.00", Region.Money(1250m));
        }

        [Fact]
        public void MoneyWhole_DropsDecimals()
        {
            Use("AED", "Dirhams", NumberWordStyle.International);
            Assert.Equal("AED 1,250", Region.MoneyWhole(1250.49m));
        }

        [Fact]
        public void MoneySigned_MarksOverAndShort()
        {
            Use("$", "Dollars", NumberWordStyle.International);
            Assert.Equal("+$ 50.00", Region.MoneySigned(50m));
            Assert.Equal("-$ 50.00", Region.MoneySigned(-50m));
        }

        [Fact]
        public void AmountInWords_SouthAsian_UsesLakhAndCrore()
        {
            Use("Rs.", "Rupees", NumberWordStyle.SouthAsian);
            Assert.Equal("One Lakh Twenty Thousand Rupees Only", Region.AmountInWords(120_000m));
            Assert.Equal("One Crore Rupees Only", Region.AmountInWords(10_000_000m));
        }

        [Fact]
        public void AmountInWords_International_UsesMillionAndBillion()
        {
            Use("$", "Dollars", NumberWordStyle.International);
            Assert.Equal("One Hundred Twenty Thousand Dollars Only", Region.AmountInWords(120_000m));
            Assert.Equal("Ten Million Dollars Only", Region.AmountInWords(10_000_000m));
            Assert.Equal("One Billion Dollars Only", Region.AmountInWords(1_000_000_000m));
        }

        [Fact]
        public void Labels_TrackTheConfiguredSymbolAndIdName()
        {
            Region.Save(new RegionSettingsData
            {
                CurrencySymbol = "£",
                CurrencyName = "Pounds",
                NationalIdLabel = "National Insurance No.",
                StatutoryDeductionLabel = "NI",
                NumberWords = NumberWordStyle.International
            });

            Assert.Equal("Amount (£)", Region.AmountLabel);
            Assert.Equal("Amount (£) *", Region.AmountLabelRequired);
            Assert.Equal("Cost Price (£)", Region.CostPriceLabel);
            Assert.Equal("NI (£)", Region.StatutoryDeductionAmountLabel);
            Assert.Equal("National Insurance No.:", Region.NationalIdLabelColon);
            Assert.Contains("National Insurance No.", Region.EmployeeSearchHint);
        }

        [Fact]
        public void Defaults_StayPakistaniSoExistingTillsAreUnchanged()
        {
            var d = new RegionSettingsData();
            Assert.Equal("Rs.", d.CurrencySymbol);
            Assert.Equal("Rupees", d.CurrencyName);
            Assert.Equal("CNIC", d.NationalIdLabel);
            Assert.Equal("EOBI", d.StatutoryDeductionLabel);
            Assert.Equal(NumberWordStyle.SouthAsian, d.NumberWords);
        }

        [Fact]
        public void SymbolPosition_CanGoAfterTheAmount()
        {
            Region.Save(new RegionSettingsData
            {
                CurrencySymbol = "kr",
                SymbolSide = SymbolPosition.After,
                NumberFormat = NumberFormatStyle.SpaceGroupCommaDecimal
            });

            Assert.Equal("1\u00a0250,00 kr", Region.Money(1250m));
        }

        [Theory]
        [InlineData(NumberFormatStyle.DotDecimal,             "1,234.56")]
        [InlineData(NumberFormatStyle.CommaDecimal,           "1.234,56")]
        [InlineData(NumberFormatStyle.SpaceGroupDotDecimal,   "1\u00a0234.56")]
        [InlineData(NumberFormatStyle.SpaceGroupCommaDecimal, "1\u00a0234,56")]
        public void NumberFormat_UsesTheConfiguredSeparators(NumberFormatStyle style, string expected)
        {
            Region.Save(new RegionSettingsData { NumberFormat = style, DecimalPlaces = 2 });
            Assert.Equal(expected, Region.Number(1234.56m));
        }

        [Theory]
        [InlineData(0, "JPY 1,251")]
        [InlineData(2, "JPY 1,250.50")]
        [InlineData(3, "JPY 1,250.500")]
        public void DecimalPlaces_AreConfigurable(int places, string expected)
        {
            Region.Save(new RegionSettingsData { CurrencySymbol = "JPY", DecimalPlaces = places });
            Assert.Equal(expected, Region.Money(1250.5m));
        }

        [Theory]
        [InlineData(DateStyle.DayFirst,   "31/12/2026", "31 Dec 2026")]
        [InlineData(DateStyle.MonthFirst, "12/31/2026", "Dec 31, 2026")]
        [InlineData(DateStyle.Iso,        "2026-12-31", "2026-Dec-31")]
        public void Dates_FollowTheConfiguredStyle(DateStyle style, string numeric, string longForm)
        {
            Region.Save(new RegionSettingsData { Dates = style });
            var d = new DateTime(2026, 12, 31, 21, 30, 0);

            Assert.Equal(numeric, Region.Date(d));
            Assert.Equal(longForm, Region.LongDate(d));
            Assert.Equal(numeric + " 09:30 PM", Region.DateTimeText(d));
        }

        [Fact]
        public void Apply_ChangesSettingsWithoutWritingToDisk()
        {
            Region.Save(new RegionSettingsData { CurrencySymbol = "Rs." });

            Region.Apply(new RegionSettingsData { CurrencySymbol = "$" });
            Assert.Equal("$ 10.00", Region.Money(10m));

            // Nothing was persisted, so a reload brings the saved symbol back.
            Region.Apply(new RegionSettingsData { CurrencySymbol = "Rs." });
            Assert.Equal("Rs. 10.00", Region.Money(10m));
        }

        [Fact]
        public void NewDefaults_MatchThePreviousHardcodedBehaviour()
        {
            var d = new RegionSettingsData();
            Assert.Equal(SymbolPosition.Before, d.SymbolSide);
            Assert.Equal(2, d.DecimalPlaces);
            Assert.Equal(NumberFormatStyle.DotDecimal, d.NumberFormat);
            Assert.Equal(DateStyle.DayFirst, d.Dates);
        }
    }
}
