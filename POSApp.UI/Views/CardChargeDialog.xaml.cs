using System.Windows;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.Views
{
    /// <summary>
    /// Sends an amount to the integrated card reader and waits while the customer taps, inserts
    /// or swipes. Closes by itself when the card is approved; on a decline the cashier can try
    /// again or cancel (and take another payment method).
    /// </summary>
    public partial class CardChargeDialog : Window
    {
        private readonly ICardTerminal _terminal;
        private readonly decimal _amount;
        private readonly string _description;
        private CancellationTokenSource? _cts;
        private bool _running;

        public CardChargeResult? Result { get; private set; }

        public CardChargeDialog(ICardTerminal terminal, decimal amount, string description)
        {
            InitializeComponent();
            _terminal = terminal;
            _amount = amount;
            _description = description;
            AmountText.Text = Region.Money(amount);
            ModeText.Text = terminal.IsTestMode ? "TEST MODE — no real money moves" : "Integrated card reader";
            TestCardButton.Visibility = terminal.IsTestMode ? Visibility.Visible : Visibility.Collapsed;
            Loaded += async (_, _) => await ChargeAsync();
            Closing += (_, e) =>
            {
                // Never leave the reader waiting for a sale this screen has given up on.
                if (_running) { _cts?.Cancel(); e.Cancel = true; }
            };
        }

        /// <summary>Charges on the reader. The approved result, or null when declined or cancelled.</summary>
        public static CardChargeResult? Run(Window owner, ICardTerminal terminal, decimal amount, string description)
        {
            var dialog = new CardChargeDialog(terminal, amount, description) { Owner = owner };
            dialog.ShowDialog();
            return dialog.Result is { Approved: true } ? dialog.Result : null;
        }

        private async Task ChargeAsync()
        {
            _running = true;
            _cts = new CancellationTokenSource();
            RetryButton.Visibility = Visibility.Collapsed;
            Busy.Visibility = Visibility.Visible;
            StatusText.Text = "Ask the customer to tap, insert or swipe their card on the reader.";
            DetailText.Text = string.Empty;
            CancelButton.IsEnabled = true;
            try
            {
                var result = await _terminal.ChargeAsync(_amount, _description, _cts.Token);
                _running = false;
                if (result.Approved)
                {
                    Result = result;
                    DialogResult = true;
                    return;
                }
                if (_cts.IsCancellationRequested) { DialogResult = false; return; }
                ShowFailure(result.Message ?? "The card was declined.");
            }
            catch (Exception ex)
            {
                _running = false;
                CrashLog.Write(ex, "Card reader");
                ShowFailure(ex.Message);
            }
        }

        private void ShowFailure(string message)
        {
            Busy.Visibility = Visibility.Collapsed;
            StatusText.Text = "Not approved";
            DetailText.Text = message + " Try again, or cancel and take another payment method.";
            RetryButton.Visibility = Visibility.Visible;
        }

        private async void Retry_Click(object sender, RoutedEventArgs e) => await ChargeAsync();

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_running)
            {
                CancelButton.IsEnabled = false;
                StatusText.Text = "Cancelling on the reader…";
                _cts?.Cancel();
                return;
            }
            DialogResult = false;
        }

        private async void TestCard_Click(object sender, RoutedEventArgs e)
        {
            try { await _terminal.SimulateCardAsync(); }
            catch (Exception ex) { DetailText.Text = "Test card: " + ex.Message; }
        }
    }
}
