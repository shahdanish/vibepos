namespace POSApp.UI.ViewModels
{
    /// <summary>Per-day scheduling indicator used by the calendar month view and week strip.</summary>
    public enum DayCallStatus
    {
        /// <summary>No calls scheduled on this date — no badge.</summary>
        None = 0,

        /// <summary>Has scheduled calls, at least one still pending — dot badge.</summary>
        HasPending = 1,

        /// <summary>Has scheduled calls and all of them are done — tick badge.</summary>
        AllDone = 2
    }
}
