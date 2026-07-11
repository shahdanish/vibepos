namespace POSApp.Core.Entities
{
    /// <summary>
    /// A scheduled call: a Medical Rep is due to call a Doctor on a given calendar date.
    /// ScheduleDate is stored as a pure DATE (no time) to avoid timezone/off-by-one issues.
    /// </summary>
    public sealed class CallSchedule
    {
        public int Id { get; set; }

        /// <summary>Calendar date of the scheduled call — DATE only, no time component.</summary>
        public DateOnly ScheduleDate { get; set; }

        public int DoctorId { get; set; }
        public Doctor? Doctor { get; set; }

        public int MedicalRepId { get; set; }
        public MedicalRep? MedicalRep { get; set; }

        public bool IsCallDone { get; set; }
        public DateTime? CallDoneAt { get; set; }
        public int? CallDoneByUserId { get; set; }

        public string? Notes { get; set; }

        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
