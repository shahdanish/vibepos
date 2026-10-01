namespace POSApp.Core.Entities
{
    /// <summary>
    /// A product starred as a "quick key" on the sale screen. Quick keys are shop-wide: every
    /// cashier sees the same tiles. <see cref="UserId"/> records who added the product.
    /// </summary>
    public sealed class UserFavorite
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public DateTime AddedDate { get; set; } = DateTime.Now;

        /// <summary>Position of the tile on the sale screen (lower first; ties by date added).</summary>
        public int SortOrder { get; set; }

        // Navigation properties
        public User? User { get; set; }
        public Product? Product { get; set; }
    }
}
