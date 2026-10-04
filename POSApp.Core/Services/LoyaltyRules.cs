namespace POSApp.Core.Services
{
    /// <summary>
    /// Loyalty points (pure). Points are earned on merchandise only: gift-card sales and the
    /// part of the bill paid with points do not earn points. Redemption is a tender, not a discount
    /// that changes the tax.
    /// </summary>
    public static class LoyaltyRules
    {
        /// <summary>
        /// Points earned on one sale. <paramref name="merchandise"/> is the sum of line totals
        /// before tax. <paramref name="giftCardAmount"/> is the face value of gift cards on the bill.
        /// <paramref name="loyaltyDollars"/> is the amount paid with points.
        /// </summary>
        public static int Earned(decimal merchandise, decimal giftCardAmount, decimal loyaltyDollars, decimal pointsPerDollar)
        {
            if (pointsPerDollar <= 0) return 0;
            var eligible = merchandise - giftCardAmount - loyaltyDollars;
            if (eligible <= 0) return 0;
            eligible = Math.Round(eligible, 2, MidpointRounding.AwayFromZero);
            return (int)Math.Floor(eligible * pointsPerDollar);
        }

        /// <summary>The most dollars <paramref name="points"/> can take off, at <paramref name="pointsPerRewardDollar"/> points per dollar.</summary>
        public static decimal MaxDollars(int points, int pointsPerRewardDollar)
        {
            if (points <= 0 || pointsPerRewardDollar <= 0) return 0;
            return Math.Round(points / (decimal)pointsPerRewardDollar, 2, MidpointRounding.ToZero);
        }

        /// <summary>
        /// Points earned on a sale. <paramref name="nonGiftLineTotals"/> is merchandise after
        /// line discounts. <paramref name="billDiscounts"/> is the extra discount on the bill.
        /// Gift-card face value is not included. The part paid with points does not earn points.
        /// </summary>
        public static int EarnedOnLines(decimal nonGiftLineTotals, decimal billDiscounts, decimal loyaltyDollars, decimal pointsPerDollar)
        {
            var net = nonGiftLineTotals - billDiscounts;
            if (net < 0) net = 0;
            return Earned(net, 0, loyaltyDollars, pointsPerDollar);
        }

        /// <summary>Points a dollar amount uses, rounded to the nearest point.</summary>
        public static int PointsFor(decimal dollars, int pointsPerRewardDollar)
        {
            if (dollars <= 0 || pointsPerRewardDollar <= 0) return 0;
            return (int)Math.Round(dollars * pointsPerRewardDollar, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Turns a requested dollar amount into a loyalty tender, capped by the point balance
        /// and by what is still due.
        /// </summary>
        public static (int Points, decimal Dollars, string? Error) Redeem(
            int pointBalance, decimal dollarsRequested, decimal remaining, int pointsPerRewardDollar)
        {
            if (pointsPerRewardDollar <= 0) return (0, 0, "Loyalty is switched off.");
            if (pointBalance <= 0) return (0, 0, "This customer has no loyalty points.");
            if (dollarsRequested <= 0) return (0, 0, "Enter an amount greater than zero.");
            if (remaining <= 0) return (0, 0, "The sale is already paid in full.");

            var max = MaxDollars(pointBalance, pointsPerRewardDollar);
            var pay = Math.Min(dollarsRequested, Math.Min(remaining, max));
            pay = Math.Round(pay, 2, MidpointRounding.AwayFromZero);
            if (pay <= 0) return (0, 0, "Not enough points for that amount.");

            var points = Math.Min(pointBalance, PointsFor(pay, pointsPerRewardDollar));
            pay = MaxDollars(points, pointsPerRewardDollar);
            if (pay <= 0 || points <= 0) return (0, 0, "Not enough points for that amount.");
            return (points, pay, null);
        }

        /// <summary>
        /// Points to give back on a return, in proportion to how much of the original tender
        /// (or of the original earned points) is being returned. Never more than the original.
        /// </summary>
        public static int PointsToRestore(int originalPoints, decimal originalDollars, decimal refundDollars)
        {
            if (originalPoints <= 0 || originalDollars <= 0 || refundDollars <= 0) return 0;
            var share = Math.Min(1m, refundDollars / originalDollars);
            var points = (int)Math.Round(originalPoints * share, MidpointRounding.AwayFromZero);
            return Math.Clamp(points, 0, originalPoints);
        }
    }
}
