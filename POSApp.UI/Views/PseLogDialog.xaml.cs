using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.UI.Helpers;

namespace POSApp.UI.Views
{
    /// <summary>
    /// Records the purchaser of pseudoephedrine products in the shop's logbook and refuses the
    /// sale when it would pass the daily or 30-day limit for that ID.
    /// </summary>
    public partial class PseLogDialog : Window
    {
        private readonly IReadOnlyList<PseCheckoutLine> _lines;
        private readonly IFrontStoreRepository _repository;

        public IReadOnlyList<PseLogEntry>? Entries { get; private set; }

        public PseLogDialog(IReadOnlyList<PseCheckoutLine> lines, IFrontStoreRepository repository)
        {
            InitializeComponent();
            _lines = lines;
            _repository = repository;
            var totalMg = lines.Sum(l => l.BaseMg);
            ItemsText.Text = string.Join(", ", lines.Select(l => $"{l.Packages:0.##} × {l.ProductName}")) +
                             $"  ·  {totalMg / 1000m:0.###} g base";
            Loaded += (_, _) => PurchaserName.Focus();
        }

        /// <summary>Shows the dialog for the sale screen. The logbook lines, or null if cancelled or refused.</summary>
        public static IReadOnlyList<PseLogEntry>? Ask(Window owner, IReadOnlyList<PseCheckoutLine> lines)
        {
            if (App.Services?.GetService<IFrontStoreRepository>() is not IFrontStoreRepository repository)
            {
                NotificationHelper.ValidationErrorCustom("The pseudoephedrine logbook is not available, so this sale can't be completed.");
                return null;
            }
            var dialog = new PseLogDialog(lines, repository) { Owner = owner };
            return dialog.ShowDialog() == true ? dialog.Entries : null;
        }

        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var name = PurchaserName.Text.Trim();
            var id = PseLogEntry.NormalizeId(IdNumber.Text);
            if (name.Length < 2 || id.Length < 4)
            {
                Show("Enter the purchaser's name and the ID number from their photo ID.", isError: true);
                return;
            }

            ConfirmButton.IsEnabled = false;
            try
            {
                var settings = await _repository.GetSettingsAsync();
                var now = AppClock.Now;
                var earlier = await _repository.GetPsePurchasesAsync(id, now.AddDays(-31));
                var check = PseLimits.Check(earlier.Select(x => (x.PurchaseDate, x.BaseMg)), _lines.Sum(l => l.BaseMg), now,
                                            settings.PseDailyLimitMg, settings.PseThirtyDayLimitMg);
                if (!check.Allowed)
                {
                    Show("Sale refused. " + check.Reason, isError: true);
                    return;
                }

                var idType = (IdType.SelectedItem as ComboBoxItem)?.Content as string ?? "Photo ID";
                Entries = _lines.Select(l => new PseLogEntry
                {
                    PurchaserName = name,
                    PurchaserAddress = string.IsNullOrWhiteSpace(PurchaserAddress.Text) ? null : PurchaserAddress.Text.Trim(),
                    IdType = idType,
                    IdNumber = id,
                    DateOfBirth = Dob.SelectedDate,
                    ProductId = l.ProductId,
                    ProductName = l.ProductName,
                    Packages = l.Packages,
                    BaseMg = l.BaseMg,
                    PurchaseDate = now
                }).ToList();
                DialogResult = true;
            }
            catch (Exception ex)
            {
                Show("Could not check the logbook: " + ex.Message, isError: true);
            }
            finally
            {
                ConfirmButton.IsEnabled = true;
            }
        }

        private void Show(string message, bool isError)
        {
            ResultText.Text = message;
            ResultPanel.Style = (Style)FindResource(isError ? "DangerCallout" : "InfoCallout");
            ResultPanel.Visibility = Visibility.Visible;
        }
    }
}
