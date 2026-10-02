using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Services;
using POSApp.UI.Helpers;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    /// <summary>
    /// Takes a US bill in several tenders: cash, card, check or the customer's charge account.
    /// Card details are what the shop's separate card terminal printed; the card number itself
    /// is never typed in.
    /// </summary>
    public partial class SplitPaymentDialog : Window
    {
        /// <summary>One added payment in the list.</summary>
        public sealed class Row
        {
            public Row(SalePayment payment) => Payment = payment;
            public SalePayment Payment { get; }
            public string Description => TenderCalculator.Describe(Payment);
            public string AmountText => Payment.Change > 0
                ? $"{Region.Money(Payment.Tendered)}  (applied {Region.Money(Payment.Amount)})"
                : Region.Money(Payment.Amount);
        }

        private readonly decimal _total;
        private readonly bool _accountAllowed;
        private readonly bool _cardReader;

        /// <summary>The most FSA/HSA cards may pay on this bill, or null when not tracked.</summary>
        private readonly decimal? _fsaLimit;

        public const string FsaCardType = "FSA/HSA card";
        private readonly ObservableCollection<Row> _rows = new();

        /// <summary>The payments, once the dialog returned true.</summary>
        public IReadOnlyList<SalePayment> Payments => _rows.Select(r => r.Payment).ToList();

        /// <param name="accountCustomer">The customer whose charge account can be used, or null.</param>
        public SplitPaymentDialog(decimal total, string? accountCustomer, IEnumerable<SalePayment>? existing = null, decimal? fsaLimit = null,
                                  bool cardReader = false)
        {
            InitializeComponent();
            _total = total;
            _fsaLimit = fsaLimit;
            _accountAllowed = accountCustomer != null;
            _cardReader = cardReader;

            TotalText.Text = $"Total due {Region.Money(total)}";
            AccountChoice.IsEnabled = _accountAllowed;
            AccountHint.Text = _accountAllowed
                ? $"Charge Account puts that part on {accountCustomer}'s account."
                : "To use a charge account, pick the customer on the sale screen first.";
            PaymentList.ItemsSource = _rows;
            foreach (var p in existing ?? Enumerable.Empty<SalePayment>())
                _rows.Add(new Row(p));

            PreviewKeyDown += OnPreviewKeyDown;
            Loaded += (_, _) => Refresh();
        }

        /// <summary>Opens the dialog for the sale screen and applies the result. True when payments were set.</summary>
        public static bool Run(Window owner, SaleViewModel vm)
        {
            if (!vm.IsUsCheckout) return false;
            if (vm.TotalBill <= 0)
            {
                NotificationHelper.ValidationErrorCustom("Add items to the bill before taking payment.");
                return false;
            }

            var dialog = new SplitPaymentDialog(vm.TotalBill, vm.SelectedCustomer?.Name, vm.SplitPayments,
                                                vm.TracksFsa ? vm.FsaEligibleTotal : null, vm.RequestCardCharge != null) { Owner = owner };
            if (dialog.ShowDialog() != true) return false;
            vm.ApplySplitPayments(dialog.Payments);
            return true;
        }

        private decimal Remaining => TenderCalculator.Remaining(_total, _rows.Select(r => r.Payment));

        private string SelectedMethod =>
            CardChoice.IsChecked == true ? PaymentMethods.Card :
            CheckChoice.IsChecked == true ? PaymentMethods.Check :
            AccountChoice.IsChecked == true ? PaymentMethods.ChargeAccount :
            PaymentMethods.Cash;

        private void Method_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            CardPanel.Visibility = CardChoice.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            CardReaderHint.Visibility = _cardReader && CardChoice.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            CheckPanel.Visibility = CheckChoice.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            FocusAmount();
        }

        private void Add_Click(object sender, RoutedEventArgs e) => AddPayment();

        private void AddPayment()
        {
            if (!decimal.TryParse(AmountBox.Text.Replace(Region.Current.CurrencySymbol, "").Trim(),
                                  NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                ShowError("Enter the amount as a number, e.g. 20 or 20.50.");
                return;
            }

            var method = SelectedMethod;
            var cardType = (CardBrand.SelectedItem as ComboBoxItem)?.Content as string;
            if (method == PaymentMethods.Card && cardType == FsaCardType && _fsaLimit is decimal limit)
            {
                var fsaSoFar = _rows.Where(r => r.Payment.CardBrand == FsaCardType).Sum(r => r.Payment.Amount);
                var room = Math.Max(0m, limit - fsaSoFar);
                if (room == 0)
                {
                    ShowError("Nothing on this bill is FSA/HSA eligible (or the eligible part is already paid). Use another card for the rest.");
                    return;
                }
                if (amount > room)
                {
                    ShowError($"An FSA/HSA card can pay only for eligible items: up to {Region.Money(room)} on this bill.");
                    return;
                }
            }
            var (payment, error) = TenderCalculator.Take(
                method, Math.Round(amount, 2), Remaining,
                cardBrand: method == PaymentMethods.Card ? (CardBrand.SelectedItem as ComboBoxItem)?.Content as string : null,
                cardLast4: method == PaymentMethods.Card ? CardLast4.Text : null,
                reference: method == PaymentMethods.Card ? AuthCode.Text : method == PaymentMethods.Check ? CheckNumber.Text : null);
            if (error != null)
            {
                ShowError(error);
                return;
            }

            _rows.Add(new Row(payment!));
            CardLast4.Clear();
            AuthCode.Clear();
            CheckNumber.Clear();
            Refresh();
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is Row row)
            {
                _rows.Remove(row);
                Refresh();
            }
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            if (Remaining > 0) return;
            DialogResult = true;
        }

        /// <summary>Enter adds the payment while something is still due, then confirms.</summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Keyboard.FocusedElement is Button) return;
            e.Handled = true;
            if (Remaining > 0) AddPayment();
            else Done_Click(this, e);
        }

        private void Refresh()
        {
            ErrorPanel.Visibility = Visibility.Collapsed;
            var remaining = Remaining;
            RemainingText.Text = Region.Money(remaining);
            EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            var change = TenderCalculator.Change(_rows.Select(r => r.Payment));
            ChangeText.Text = $"Change due {Region.Money(change)}";
            ChangeText.Visibility = change > 0 ? Visibility.Visible : Visibility.Collapsed;

            DoneButton.IsEnabled = remaining == 0 && _rows.Count > 0;
            AddButton.IsEnabled = remaining > 0;
            AmountBox.Text = remaining > 0 ? remaining.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty;
            if (remaining > 0) FocusAmount();
            else DoneButton.Focus();
        }

        private void FocusAmount()
        {
            AmountBox.Focus();
            AmountBox.SelectAll();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
            FocusAmount();
        }
    }
}
