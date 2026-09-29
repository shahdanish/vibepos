using POSApp.UI.Helpers;
using Xunit;

namespace POSApp.Tests
{
    /// <summary>
    /// Guards the currency/region layer that lets one build ship to any country.
    /// These run against whatever is saved on the machine, so each test sets the
    /// settings it needs and restores the original afterwards.
    /// </summary>
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
    }
}
