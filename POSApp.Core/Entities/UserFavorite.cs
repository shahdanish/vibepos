namespace POSApp.Core.Entities
{
    public sealed class UserFavorite
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public DateTime AddedDate { get; set; } = DateTime.Now;
        
        // Navigation properties
        public User? User { get; set; }
        public Product? Product { get; set; }
    }
}
