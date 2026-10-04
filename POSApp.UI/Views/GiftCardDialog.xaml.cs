using System.Globalization;
using System.Windows;
using POSApp.UI.Helpers;

namespace POSApp.UI.Views
{
    /// <summary>Sells a store gift card onto the open sale. The code is created when the line is added.</summary>
    public partial class GiftCardDialog : Window
    {
        public decimal Amount { get; private set; }
        public string? Recipient { get; private set; }

        public GiftCardDialog()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                AmountBox.Focus();
                AmountBox.SelectAll();
            };
        }

        /// <summary>True when the cashier added a card. Amount is greater than zero.</summary>
        public static bool TryAsk(Window owner, out decimal amount, out string? recipient)
        {
            var dialog = new GiftCardDialog { Owner = owner };
            if (dialog.ShowDialog() != true)
            {
                amount = 0;
                recipient = null;
                return false;
            }
            amount = dialog.Amount;
            recipient = dialog.Recipient;
            return true;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(AmountBox.Text.Replace(Region.Current.CurrencySymbol, "").Trim(),
                                  NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            {
                ErrorText.Text = "Enter an amount greater than zero.";
                ErrorText.Visibility = Visibility.Visible;
                AmountBox.Focus();
                return;
            }

            Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            Recipient = string.IsNullOrWhiteSpace(NameBox.Text) ? null : NameBox.Text.Trim();
            DialogResult = true;
        }
    }
}
