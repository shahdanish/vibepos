using POSApp.Core.Entities;

namespace POSApp.Core.Services
{
    /// <summary>
    /// The rules for taking payment on a US sale (pure). Card, check and charge-account tenders
    /// can pay at most what is still due. Cash can be more, and the rest is handed back as change.
    /// </summary>
    public static class TenderCalculator
    {
        /// <summary>What is still to be paid after the tenders so far (never below 0).</summary>
        public static decimal Remaining(decimal total, IEnumerable<SalePayment> payments) =>
            Math.Max(0m, total - payments.Sum(p => p.Amount));

        /// <summary>Change handed back across all cash tenders.</summary>
        public static decimal Change(IEnumerable<SalePayment> payments) => payments.Sum(p => p.Change);

        /// <summary>
        /// Builds the tender for <paramref name="amount"/> towards <paramref name="remaining"/>, or
        /// explains why it can't be taken.
        /// </summary>
        public static (SalePayment? Payment, string? Error) Take(
            string method, decimal amount, decimal remaining,
            string? cardBrand = null, string? cardLast4 = null, string? reference = null)
        {
            if (remaining <= 0) return (null, "The sale is already paid in full.");
            if (amount <= 0) return (null, "Enter an amount greater than zero.");
            if (method != PaymentMethods.Cash && amount > remaining)
                return (null, $"A {method.ToLowerInvariant()} payment can't be more than the amount due. Only cash gives change.");

            var last4 = string.IsNullOrWhiteSpace(cardLast4) ? null : cardLast4.Trim();
            if (last4 != null && (last4.Length != 4 || !last4.All(char.IsAsciiDigit)))
                return (null, "Card last 4 digits must be exactly four numbers.");

            return (new SalePayment
            {
                Method = method,
                Amount = Math.Min(amount, remaining),
                Tendered = amount,
                CardBrand = string.IsNullOrWhiteSpace(cardBrand) ? null : cardBrand.Trim(),
                CardLast4 = last4,
                Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim()
            }, null);
        }

        /// <summary>
        /// The tender for a single-method US sale (no split). Cash with nothing typed means the
        /// customer paid the exact amount; cash short of the total is refused.
        /// </summary>
        public static (SalePayment? Payment, string? Error) Single(string method, decimal total, decimal? cashReceived)
        {
            if (total <= 0) return (null, null);
            if (method == PaymentMethods.Cash)
            {
                var given = cashReceived is > 0 ? cashReceived.Value : total;
                if (given < total)
                    return (null, "Cash received is less than the total. Use Split to take the rest by card, or choose Charge Account.");
                return Take(method, given, total);
            }
            return Take(method, total, total);
        }

        /// <summary>One receipt/summary line for a tender, e.g. "Card (Visa ****4242)".</summary>
        public static string Describe(SalePayment p)
        {
            var details = new List<string>();
            if (!string.IsNullOrEmpty(p.CardBrand)) details.Add(p.CardBrand!);
            if (!string.IsNullOrEmpty(p.CardLast4)) details.Add("****" + p.CardLast4);
            if (!string.IsNullOrEmpty(p.Reference))
                details.Add(p.Method == PaymentMethods.Check ? "#" + p.Reference : "Auth " + p.Reference);
            return details.Count == 0 ? p.Method : $"{p.Method} ({string.Join(" ", details)})";
        }
    }
}
