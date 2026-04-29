namespace POSApp.Core.Entities
{
    public sealed class CustomerPayment
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public decimal AmountPaid { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.Now;
        public string? Note { get; set; }
        
        public Customer? Customer { get; set; }
    }
}
