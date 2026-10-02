namespace POSApp.Core.Entities
{
    /// <summary>
    /// A sales-tax class for products, with the combined (state + local) rate the shop charges
    /// on it. A US front store usually keeps general merchandise taxable, prescriptions exempt,
    /// and, depending on the state, non-prescription drugs or groceries reduced or exempt.
    /// </summary>
    public sealed class TaxCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>Percent, e.g. 8.25 for 8.25%. 0 means not taxed.</summary>
        public decimal RatePercent { get; set; }

        /// <summary>Applies to every product that has no category of its own. One category is the default.</summary>
        public bool IsDefault { get; set; }

        public int SortOrder { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
