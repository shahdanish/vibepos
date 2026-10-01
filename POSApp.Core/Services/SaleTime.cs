namespace POSApp.Core.Services
{
    /// <summary>
    /// Decides the moment a sale is recorded. Sale screens are hidden rather than closed
    /// between sales, so the date they show can be hours (or a day) old by the time the next
    /// sale is saved; recording that shown value would put sales in the wrong shift and day.
    /// </summary>
    public static class SaleTime
    {
        /// <param name="shown">The date on the sale screen.</param>
        /// <param name="chosenByUser">True when the cashier picked that date themselves.</param>
        /// <param name="now">The current time.</param>
        /// <returns>
        /// <paramref name="now"/>, unless the cashier deliberately back- or forward-dated the
        /// sale to another day — then that day at the current time of day.
        /// </returns>
        public static DateTime Resolve(DateTime shown, bool chosenByUser, DateTime now) =>
            chosenByUser && shown.Date != now.Date
                ? shown.Date + now.TimeOfDay
                : now;
    }
}
