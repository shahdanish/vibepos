using POSApp.Core.Entities;

namespace POSApp.Core.Services
{
    /// <summary>
    /// How much of a sale went through the cash drawer. For a US sale it is the cash tenders
    /// only (a cash refund is negative); card, check and charge-account money never reaches the
    /// drawer. A sale with no tenders recorded (every Pakistani sale, and anything older) keeps
    /// the figure its screen has always used, so those totals do not move.
    /// </summary>
    public static class DrawerCash
    {
        public static decimal For(Sale sale, Func<Sale, decimal> withoutTenders) =>
            sale.Payments.Count > 0
                ? sale.Payments.Where(p => p.Method == PaymentMethods.Cash).Sum(p => p.Amount)
                : withoutTenders(sale);
    }
}
