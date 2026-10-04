using System.Security.Cryptography;
using System.Text.RegularExpressions;
using POSApp.Core.Entities;

namespace POSApp.Core.Services
{
    /// <summary>
    /// Gift-card amounts (pure). A card is store credit: the register never stores a bank card
    /// number. The code is printed on the receipt and typed back in when the customer spends it.
    /// </summary>
    public static class GiftCardRules
    {
        /// <summary>Sale line product id for a card being sold. It is not a stock item.</summary>
        public const string LineProductId = "GIFT-CARD";

        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        /// <summary>A new code, e.g. GC4K7M2Q9P. Ambiguous characters (0/O, 1/I) are left out.</summary>
        public static string NewCode()
        {
            Span<char> chars = stackalloc char[8];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            return "GC" + new string(chars);
        }

        public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

        private static readonly Regex CodePattern = new("GC[" + Alphabet + "]{8}", RegexOptions.Compiled);

        /// <summary>The code printed in a sale line, or null when the line has none.</summary>
        public static string? CodeIn(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var match = CodePattern.Match(text.ToUpperInvariant());
            return match.Success ? match.Value : null;
        }

        /// <summary>Puts a refund back on a card that was used to pay. The card must already have been found.</summary>
        public static void Credit(GiftCard card, decimal amount)
        {
            card.Balance = Math.Round(card.Balance + Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Takes back a card that was sold, because the sale of that card is being returned.
        /// Fails when the card has already been spent below <paramref name="face"/>.
        /// </summary>
        public static string? ReverseIssue(GiftCard card, decimal face)
        {
            face = Math.Round(Math.Abs(face), 2, MidpointRounding.AwayFromZero);
            if (card.IsVoid) return $"Gift card {card.Code} has been voided, so it cannot be returned.";
            if (face <= 0) return "Enter an amount greater than zero.";
            if (card.Balance + 0.001m < face)
                return $"Gift card {card.Code} has {card.Balance:0.00} left because it was already used. A return can put back at most that.";
            card.Balance = Math.Round(card.Balance - face, 2, MidpointRounding.AwayFromZero);
            return null;
        }

        /// <summary>How much of <paramref name="requested"/> this card can pay toward <paramref name="remaining"/>.</summary>
        public static (decimal Pay, string? Error) Redeemable(decimal balance, bool isVoid, decimal requested, decimal remaining)
        {
            if (isVoid) return (0, "This gift card has been voided.");
            if (balance <= 0) return (0, "This gift card has no balance left.");
            if (requested <= 0) return (0, "Enter an amount greater than zero.");
            if (remaining <= 0) return (0, "The sale is already paid in full.");
            var pay = Math.Min(requested, Math.Min(balance, remaining));
            pay = Math.Round(pay, 2, MidpointRounding.AwayFromZero);
            if (pay <= 0) return (0, "This gift card cannot pay that amount.");
            return (pay, null);
        }
    }
}
