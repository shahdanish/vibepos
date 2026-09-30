using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;

namespace POSApp.UI.Views
{
    /// <summary>
    /// Explains what Swifttill Pro unlocks, lists the subscription plans on sale in the
    /// Microsoft Store (monthly / yearly, with a trial if configured) and starts the purchase.
    /// </summary>
    public partial class UpgradeWindow : Window
    {
        private readonly IEditionService _edition;
        private string? _selectedOfferId;
        private bool _firstPlanSelected;

        public UpgradeWindow(IEditionService edition, AppFeature? requestedFeature)
        {
            InitializeComponent();
            _edition = edition;

            if (requestedFeature is { } f)
                TitleText.Text = $"{EditionPolicy.DisplayName(f)} is part of Swifttill Pro";

            FeatureList.ItemsSource = EditionPolicy.ProFeatures
                .Select(EditionPolicy.DisplayName)
                .ToList();

            Loaded += async (_, _) => await LoadPlansAsync();
        }

        private async Task LoadPlansAsync()
        {
            var offers = await _edition.GetProOffersAsync();
            LoadingText.Visibility = Visibility.Collapsed;

            if (offers.Count == 0)
            {
                ShowMessage("Plans couldn't be loaded. Check your internet connection and that you're signed in to the Microsoft Store, then try again.");
                return;
            }

            PlanList.ItemsSource = offers;
        }

        /// <summary>Pre-selects the first plan so one click subscribes.</summary>
        private void Plan_Loaded(object sender, RoutedEventArgs e)
        {
            if (_firstPlanSelected || sender is not RadioButton rb) return;
            _firstPlanSelected = true;
            rb.IsChecked = true;
        }

        private void Plan_Checked(object sender, RoutedEventArgs e)
        {
            _selectedOfferId = (sender as RadioButton)?.Tag as string;
            UpgradeButton.IsEnabled = _selectedOfferId != null;
        }

        private async void Upgrade_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedOfferId == null) return;

            UpgradeButton.IsEnabled = LaterButton.IsEnabled = false;
            UpgradeButton.Content = "Opening Store…";
            MessagePanel.Visibility = Visibility.Collapsed;

            var hwnd = new WindowInteropHelper(this).Handle;
            var result = await _edition.RequestUpgradeAsync(hwnd, _selectedOfferId);

            switch (result)
            {
                case UpgradeResult.Purchased:
                case UpgradeResult.AlreadyOwned:
                    DialogResult = true;
                    return;
                case UpgradeResult.Cancelled:
                    break;
                case UpgradeResult.NotAvailable:
                    ShowMessage("This plan isn't available right now. Please try again later.");
                    break;
                default:
                    ShowMessage("The Store couldn't complete the purchase. Check your internet connection and try again.");
                    break;
            }

            UpgradeButton.IsEnabled = LaterButton.IsEnabled = true;
            UpgradeButton.Content = "Subscribe";
        }

        private void ShowMessage(string text)
        {
            MessageText.Text = text;
            MessagePanel.Visibility = Visibility.Visible;
        }

        private void Later_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
