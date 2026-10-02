namespace POSApp.Core.Entities
{
    /// <summary>
    /// One line of the shop's pseudoephedrine / ephedrine logbook: who bought which product,
    /// how much base drug, and who sold it. Kept locally only — the program does not connect to
    /// any state tracking system.
    /// </summary>
    public sealed class PseLogEntry
    {
        public int Id { get; set; }
        public int? SaleId { get; set; }
        public DateTime PurchaseDate { get; set; }

        public string PurchaserName { get; set; } = string.Empty;
        public string? PurchaserAddress { get; set; }

        /// <summary>e.g. "Driver license", "State ID", "Passport".</summary>
        public string IdType { get; set; } = string.Empty;

        /// <summary>Upper-case, without spaces or dashes, so the same ID always matches.</summary>
        public string IdNumber { get; set; } = string.Empty;

        public DateTime? DateOfBirth { get; set; }

        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;

        /// <summary>Packages sold (negative on a return).</summary>
        public decimal Packages { get; set; }

        /// <summary>Total base drug in milligrams (packages × base per package).</summary>
        public decimal BaseMg { get; set; }

        /// <summary>Username of the cashier who made the sale.</summary>
        public string RecordedBy { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public Sale? Sale { get; set; }

        public static string NormalizeId(string? id) =>
            new string((id ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
