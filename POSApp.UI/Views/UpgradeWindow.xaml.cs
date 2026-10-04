using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.UI.Helpers;

namespace POSApp.UI.Views
{
    /// <summary>
    /// Explains what Swifttill Pro unlocks, lists the subscription plans on sale in the
    /// Microsoft Store (monthly / yearly, with a trial if configured) and starts the purchase.
    /// A failed catalog load shows the Store's cause. It never unlocks Pro by itself.
    /// </summary>
    public partial class UpgradeWindow : Window
    {
        private readonly IEditionService _edition;
        private string? _selectedOfferId;
        private bool _firstPlanSelected;
        private bool _loading;

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

        private async void Retry_Click(object sender, RoutedEventArgs e) => await LoadPlansAsync();

        private async Task LoadPlansAsync()
        {
            if (_loading) return;
            _loading = true;
            ShowLoading();

            try
            {
                var hwnd = new WindowInteropHelper(this).EnsureHandle();
                var query = await _edition.GetProOffersAsync(hwnd);
                ShowQuery(query);
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex, "UpgradeWindow.LoadPlans");
                ShowQuery(new ProOfferQuery(
                    Array.Empty<ProOffer>(),
                    StorePlanClassifier.FromException(ex),
                    StorePlanClassifier.FormatCode(ex.HResult)));
            }
            finally
            {
                _loading = false;
                LoadingText.Visibility = Visibility.Collapsed;
                RetryButton.IsEnabled = true;
            }
        }

        private void ShowLoading()
        {
            _selectedOfferId = null;
            _firstPlanSelected = false;
            PlanList.ItemsSource = null;
            PlanHeading.Visibility = Visibility.Collapsed;
            CancelHint.Visibility = Visibility.Collapsed;
            MessagePanel.Visibility = Visibility.Collapsed;
            MessageText.Text = "";
            ErrorCodeText.Text = "";
            ErrorCodeText.Visibility = Visibility.Collapsed;
            SignInButton.Visibility = Visibility.Collapsed;
            SupportLink.Visibility = Visibility.Collapsed;
            LoadingText.Visibility = Visibility.Visible;
            UpgradeButton.IsEnabled = false;
            UpgradeButton.Content = "Subscribe";
            RetryButton.IsEnabled = false;
        }

        private void ShowQuery(ProOfferQuery query)
        {
            if (query.Offers.Count > 0)
            {
                MessagePanel.Visibility = Visibility.Collapsed;
                SignInButton.Visibility = Visibility.Collapsed;
                SupportLink.Visibility = Visibility.Collapsed;
                ErrorCodeText.Visibility = Visibility.Collapsed;
                PlanHeading.Visibility = Visibility.Visible;
                CancelHint.Visibility = Visibility.Visible;
                _firstPlanSelected = false;
                PlanList.ItemsSource = query.Offers;
                return;
            }

            PlanList.ItemsSource = null;
            PlanHeading.Visibility = Visibility.Collapsed;
            CancelHint.Visibility = Visibility.Collapsed;
            UpgradeButton.IsEnabled = false;

            var problem = query.Problem == StorePlanProblem.None
                ? StorePlanProblem.CatalogEmpty
                : query.Problem;
            MessageText.Text = StorePlanMessages.For(problem);
            MessagePanel.Visibility = Visibility.Visible;

            var showCode = problem == StorePlanProblem.Other && !string.IsNullOrEmpty(query.ErrorCode);
            ErrorCodeText.Text = showCode ? StorePlanMessages.ErrorCodeLine(query.ErrorCode!) : "";
            ErrorCodeText.Visibility = showCode ? Visibility.Visible : Visibility.Collapsed;
            SignInButton.Visibility = problem == StorePlanProblem.NotSignedIn ? Visibility.Visible : Visibility.Collapsed;
            SupportLink.Visibility = problem is StorePlanProblem.CatalogEmpty or StorePlanProblem.StoreUnavailable
                ? Visibility.Visible
                : Visibility.Collapsed;
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
            if (_loading || _selectedOfferId == null) return;

            UpgradeButton.IsEnabled = LaterButton.IsEnabled = RetryButton.IsEnabled = false;
            UpgradeButton.Content = "Opening Store…";
            MessagePanel.Visibility = Visibility.Collapsed;

            var hwnd = new WindowInteropHelper(this).Handle;
            UpgradeResult result;
            try
            {
                result = await _edition.RequestUpgradeAsync(hwnd, _selectedOfferId);
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex, "UpgradeWindow.Purchase");
                result = UpgradeResult.Failed;
            }

            switch (result)
            {
                case UpgradeResult.Purchased:
                case UpgradeResult.AlreadyOwned:
                    DialogResult = true;
                    return;
                case UpgradeResult.Cancelled:
                    break;
                case UpgradeResult.NotAvailable:
                    ShowInline("This plan isn't available right now. Please try again later.");
                    break;
                default:
                    ShowInline("The Store couldn't complete the purchase. Try again.");
                    break;
            }

            UpgradeButton.IsEnabled = _selectedOfferId != null;
            LaterButton.IsEnabled = RetryButton.IsEnabled = true;
            UpgradeButton.Content = "Subscribe";
        }

        private void ShowInline(string text)
        {
            MessageText.Text = text;
            ErrorCodeText.Visibility = Visibility.Collapsed;
            SignInButton.Visibility = Visibility.Collapsed;
            SupportLink.Visibility = Visibility.Collapsed;
            MessagePanel.Visibility = Visibility.Visible;
        }

        private void SignIn_Click(object sender, RoutedEventArgs e)
        {
            if (!TryOpen("ms-windows-store://signin"))
                TryOpen("ms-windows-store://home");
        }

        private void Support_Click(object sender, RoutedEventArgs e) =>
            TryOpen(StoreSupportContact.PlansDidNotLoadLink.AbsoluteUri);

        private static bool TryOpen(string uri)
        {
            try
            {
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex, "UpgradeWindow.OpenLink");
                return false;
            }
        }

        private void Later_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
