using System.Windows;
using System.Windows.Input;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.Views
{
    /// <summary>
    /// Blocking gate shown at startup when the yearly license has expired (or the system
    /// clock was rolled back). The app continues only if the user enters a valid renewal
    /// code; otherwise it exits.
    /// </summary>
    public partial class LicenseExpiredWindow : Window
    {
        private readonly ILicenseService _licenseService;

        /// <summary>True once a valid renewal code has been applied — the app may continue.</summary>
        public bool Renewed { get; private set; }

        public LicenseExpiredWindow(ILicenseService licenseService, LicenseStatus status)
        {
            InitializeComponent();
            _licenseService = licenseService;

            txtContact.Text = licenseService.RenewalContactMessage;
            txtInstructions.Text = licenseService.RenewalInstructions;
            txtActivationId.Text = status.ActivationId;

            if (status.State == LicenseState.Tampered)
            {
                txtHeadline.Text = "Date/Time Problem";
                txtMessage.Text = "The system clock appears to have been changed. " +
                                  "Please set the correct date and time and restart the software, " +
                                  "or enter a renewal code below.";
            }

            Loaded += (_, _) => txtCode.Focus();
        }

        private void TxtCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                TryActivate();
            }
        }

        private void BtnActivate_Click(object sender, RoutedEventArgs e) => TryActivate();

        private void TryActivate()
        {
            var code = txtCode.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                ShowResult("Please enter the renewal code.", success: false);
                return;
            }

            var status = _licenseService.Renew(code);
            if (status is null)
            {
                ShowResult("Invalid renewal code. Please check it and try again.", success: false);
                return;
            }

            Renewed = true;
            var expiry = Region.LongDate(status.ExpiryUtc.ToLocalTime());
            MessageBox.Show(
                $"Thank you! Your license has been renewed and is now valid until {expiry}.",
                "License Renewed", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowResult(string message, bool success)
        {
            txtResult.Text = message;
            txtResult.Foreground = success
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.Red;
            txtResult.Visibility = Visibility.Visible;
        }
    }
}
