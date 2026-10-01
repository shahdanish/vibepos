using System.Threading;
using POSApp.UI.Helpers;
using POSApp.UI.Views;
using Xunit;
using POSApp.Core.Services;

namespace POSApp.Tests
{
    /// <summary>
    /// Constructs the admin settings window for real. A build only checks that the XAML
    /// compiles; this catches what it cannot — an x:Name that does not match the code-behind,
    /// a markup extension that throws, or a missing resource — without anyone logging in.
    /// </summary>
    [Collection(GlobalStateCollection.Name)]
    public class BusinessSettingsWindowTests
    {
        /// <summary>WPF windows can only be created on an STA thread, which xUnit does not provide.</summary>
        private static void OnStaThread(Action action)
        {
            Exception? failure = null;
            var t = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { failure = ex; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(TimeSpan.FromSeconds(30));

            if (failure != null)
                throw new Xunit.Sdk.XunitException($"Window failed to load: {failure}");
        }

        [Fact]
        public void Window_LoadsAndShowsTheSavedSettings()
        {
            var original = Region.Current;
            try
            {
                Region.Apply(new RegionSettingsData
                {
                    CurrencySymbol = "AED",
                    CurrencyName = "Dirhams",
                    NationalIdLabel = "Emirates ID",
                    StatutoryDeductionLabel = "Pension",
                    NumberWords = NumberWordStyle.International,
                    Dates = DateStyle.MonthFirst,
                    DecimalPlaces = 2
                });

                OnStaThread(() =>
                {
                    var w = new BusinessSettingsWindow();

                    // Fields are populated from the settings, not left blank.
                    Assert.Equal("AED", w.FindName("txtCurrencySymbol") is System.Windows.Controls.TextBox t ? t.Text : null);
                    Assert.Equal("Emirates ID",
                        w.FindName("txtNationalIdLabel") is System.Windows.Controls.TextBox n ? n.Text : null);

                    // The preview rendered with the configured currency and US date order.
                    var total = w.FindName("txtPreviewTotal") as System.Windows.Controls.TextBlock;
                    Assert.NotNull(total);
                    Assert.Contains("AED", total!.Text);

                    var date = w.FindName("txtPreviewDate") as System.Windows.Controls.TextBlock;
                    Assert.NotNull(date);
                    Assert.Contains("12/31/2026", date!.Text);

                    w.Close();
                });
            }
            finally
            {
                Region.Apply(original);
            }
        }

        [Fact]
        public void Preview_DoesNotLeakUnsavedSettingsIntoTheApp()
        {
            var saved = new RegionSettingsData { CurrencySymbol = "Rs." };
            Region.Apply(saved);

            OnStaThread(() =>
            {
                var w = new BusinessSettingsWindow();
                w.Close();
            });

            // The window renders its preview by swapping settings in and out again;
            // once it is done the app must be back on the saved currency.
            Assert.Equal("Rs. 10.00", Region.Money(10m));
        }
    }
}
