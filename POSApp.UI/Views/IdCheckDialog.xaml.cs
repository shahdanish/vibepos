using System.Windows;
using System.Windows.Controls;
using POSApp.Core.Services;
using POSApp.UI.Helpers;

namespace POSApp.UI.Views
{
    /// <summary>
    /// Age check for a restricted item: the cashier reads the date of birth off the customer's
    /// photo ID. Only the age decision is kept on the sale, not the date of birth.
    /// </summary>
    public partial class IdCheckDialog : Window
    {
        private readonly int _minimumAge;

        public DateTime? DateOfBirth => Dob.SelectedDate;

        public IdCheckDialog(int minimumAge, string productName)
        {
            InitializeComponent();
            _minimumAge = minimumAge;
            Heading.Text = $"ID check — {minimumAge}+ only";
            ProductText.Text = productName;
            Dob.DisplayDate = AppClock.Now.Date.AddYears(-minimumAge - 5);
            Loaded += (_, _) => Dob.Focus();
        }

        /// <summary>Shows the dialog; the date of birth when the cashier confirmed, else null.</summary>
        public static DateTime? Ask(Window owner, int minimumAge, string productName)
        {
            var dialog = new IdCheckDialog(minimumAge, productName) { Owner = owner };
            return dialog.ShowDialog() == true ? dialog.DateOfBirth : null;
        }

        private void Dob_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (Dob.SelectedDate is not DateTime dob || dob > AppClock.Now.Date)
            {
                AgeText.Text = string.Empty;
                ConfirmButton.IsEnabled = false;
                return;
            }
            var age = AgeCheck.AgeOn(dob, AppClock.Now);
            var ok = age >= _minimumAge;
            AgeText.Text = ok ? $"Age {age} — OK to sell" : $"Age {age} — under {_minimumAge}, do not sell";
            AgeText.Foreground = (System.Windows.Media.Brush)FindResource(ok ? "SuccessTextBrush" : "DangerTextBrush");
            // The sale screen refuses an under-age customer with its own message, so confirming is
            // allowed either way: the cashier always sees why.
            ConfirmButton.IsEnabled = true;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}
