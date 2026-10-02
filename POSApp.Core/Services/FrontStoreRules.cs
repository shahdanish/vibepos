namespace POSApp.Core.Services
{
    /// <summary>Age from a date of birth, for ID checks at the register (pure).</summary>
    public static class AgeCheck
    {
        /// <summary>
        /// Completed years on <paramref name="on"/> (a birthday counts from that day). Someone born
        /// on 29 February has their birthday on 1 March in other years — the cautious reading for
        /// an age-restricted sale.
        /// </summary>
        public static int AgeOn(DateTime dateOfBirth, DateTime on)
        {
            var age = on.Year - dateOfBirth.Year;
            var birthdayThisYear = dateOfBirth.Month == 2 && dateOfBirth.Day == 29 && !DateTime.IsLeapYear(on.Year)
                ? new DateTime(on.Year, 3, 1)
                : new DateTime(on.Year, dateOfBirth.Month, dateOfBirth.Day);
            if (on.Date < birthdayThisYear) age--;
            return age;
        }

        public static bool IsOldEnough(DateTime dateOfBirth, int minimumAge, DateTime on) =>
            minimumAge <= 0 || AgeOn(dateOfBirth, on) >= minimumAge;
    }

    /// <summary>The outcome of checking a pseudoephedrine purchase against the limits.</summary>
    public sealed record PseCheckResult(bool Allowed, decimal TodayMg, decimal ThirtyDayMg, string? Reason);

    /// <summary>
    /// Pseudoephedrine / ephedrine purchase limits for one purchaser, checked against the shop's
    /// own logbook (pure). Defaults are the federal per-day and 30-day amounts of base drug; a
    /// shop in a stricter state lowers them in Business Settings → Pharmacy. Purchases at other
    /// stores are not visible here.
    /// </summary>
    public static class PseLimits
    {
        public const decimal FederalDailyMg = 3600m;
        public const decimal FederalThirtyDayMg = 9000m;

        /// <param name="earlier">The purchaser's earlier logbook lines: when, and base mg.</param>
        public static PseCheckResult Check(IEnumerable<(DateTime When, decimal BaseMg)> earlier, decimal thisSaleMg,
                                           DateTime now, decimal dailyLimitMg = FederalDailyMg, decimal thirtyDayLimitMg = FederalThirtyDayMg)
        {
            var list = earlier.ToList();
            var today = list.Where(e => e.When.Date == now.Date).Sum(e => e.BaseMg) + thisSaleMg;
            var thirty = list.Where(e => e.When > now.AddDays(-30)).Sum(e => e.BaseMg) + thisSaleMg;

            if (today > dailyLimitMg)
                return new(false, today, thirty,
                    $"This would bring today's total to {today / 1000m:0.###} g; the limit is {dailyLimitMg / 1000m:0.###} g a day.");
            if (thirty > thirtyDayLimitMg)
                return new(false, today, thirty,
                    $"This would bring the last 30 days to {thirty / 1000m:0.###} g; the limit is {thirtyDayLimitMg / 1000m:0.###} g in 30 days.");
            return new(true, today, thirty, null);
        }
    }

    /// <summary>A cart line for the FSA/HSA calculation.</summary>
    public sealed record FsaLine(bool Eligible, decimal Taxable, decimal Tax);

    /// <summary>What an FSA/HSA card may pay for on a bill: the eligible items and their tax (pure).</summary>
    public static class FsaEligibility
    {
        public static decimal EligibleAmount(IEnumerable<FsaLine> lines) =>
            lines.Where(l => l.Eligible).Sum(l => l.Taxable + l.Tax);
    }

    /// <summary>A pseudoephedrine line at checkout, for the logbook dialog.</summary>
    public sealed record PseCheckoutLine(string ProductId, string ProductName, decimal Packages, decimal BaseMg);
}
