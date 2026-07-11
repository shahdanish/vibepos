using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class CallScheduleViewModel : ViewModelBase
    {
        private readonly ICallScheduleRepository _scheduleRepo;
        private readonly IDoctorRepository _doctorRepo;
        private readonly IMedicalRepRepository _repRepo;
        private readonly ICurrentUserContext _user;

        private DateTime _selectedDate = DateTime.Today;
        private DateTime _displayMonth = DateTime.Today;
        private bool _isWeekView;
        private Doctor? _selectedDoctor;
        private MedicalRep? _selectedRep;
        private string? _newNotes;
        private IReadOnlyDictionary<DateOnly, DayCallStatus> _dayStatusMap = new Dictionary<DateOnly, DayCallStatus>();

        public ObservableCollection<Doctor> Doctors { get; } = new();
        public ObservableCollection<MedicalRep> MedicalReps { get; } = new();
        public ObservableCollection<CallScheduleRow> Entries { get; } = new();
        public ObservableCollection<DayCellViewModel> WeekDays { get; } = new();

        /// <summary>Map consumed by the month-calendar day badges (via multi-value converters).</summary>
        public IReadOnlyDictionary<DateOnly, DayCallStatus> DayStatusMap
        {
            get => _dayStatusMap;
            private set => SetProperty(ref _dayStatusMap, value);
        }

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    OnPropertyChanged(nameof(SelectedDateLabel));
                    OnPropertyChanged(nameof(WeekRangeLabel));
                    _ = OnSelectedDateChangedAsync();
                }
            }
        }

        /// <summary>The month the calendar is displaying — drives the badge range on month navigation.</summary>
        public DateTime DisplayMonth
        {
            get => _displayMonth;
            set
            {
                if (SetProperty(ref _displayMonth, value))
                    _ = LoadPeriodAsync();
            }
        }

        public string SelectedDateLabel => SelectedDate.ToString("dddd, dd MMM yyyy");

        public bool IsWeekView
        {
            get => _isWeekView;
            set
            {
                if (SetProperty(ref _isWeekView, value))
                {
                    OnPropertyChanged(nameof(IsMonthView));
                    OnPropertyChanged(nameof(ViewToggleLabel));
                    _ = LoadPeriodAsync();
                }
            }
        }

        public bool IsMonthView => !_isWeekView;
        public string ViewToggleLabel => _isWeekView ? "📅 Switch to Month" : "🗓 Switch to Week";

        public string WeekRangeLabel
        {
            get
            {
                var (start, end) = WeekBounds(DateOnly.FromDateTime(SelectedDate));
                return $"{start:dd MMM} – {end:dd MMM yyyy}";
            }
        }

        public Doctor? SelectedDoctor
        {
            get => _selectedDoctor;
            set => SetProperty(ref _selectedDoctor, value);
        }

        public MedicalRep? SelectedRep
        {
            get => _selectedRep;
            set => SetProperty(ref _selectedRep, value);
        }

        public string? NewNotes
        {
            get => _newNotes;
            set => SetProperty(ref _newNotes, value);
        }

        public ICommand SaveScheduleCommand { get; }
        public ICommand MarkDoneCommand { get; }
        public ICommand AddDoctorCommand { get; }
        public ICommand AddMedicalRepCommand { get; }
        public ICommand ToggleViewCommand { get; }
        public ICommand PrevPeriodCommand { get; }
        public ICommand NextPeriodCommand { get; }
        public ICommand RefreshCommand { get; }

        /// <summary>Raised so the view can open the (reused) Doctor form dialog.</summary>
        public event Action? AddDoctorRequested;
        /// <summary>Raised so the view can open the Medical Rep form dialog.</summary>
        public event Action? AddMedicalRepRequested;

        public CallScheduleViewModel(
            ICallScheduleRepository scheduleRepo,
            IDoctorRepository doctorRepo,
            IMedicalRepRepository repRepo,
            ICurrentUserContext user)
        {
            _scheduleRepo = scheduleRepo;
            _doctorRepo = doctorRepo;
            _repRepo = repRepo;
            _user = user;

            SaveScheduleCommand = new RelayCommand(async _ => await SaveScheduleAsync());
            MarkDoneCommand = new RelayCommand(async p => await MarkDoneAsync(p as CallScheduleRow), p => p is CallScheduleRow r && r.CanMarkDone);
            AddDoctorCommand = new RelayCommand(_ => AddDoctorRequested?.Invoke());
            AddMedicalRepCommand = new RelayCommand(_ => AddMedicalRepRequested?.Invoke());
            ToggleViewCommand = new RelayCommand(_ => IsWeekView = !IsWeekView);
            PrevPeriodCommand = new RelayCommand(_ => ShiftPeriod(-1));
            NextPeriodCommand = new RelayCommand(_ => ShiftPeriod(+1));
            RefreshCommand = new RelayCommand(async _ => await RefreshAllAsync());

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await ReloadDoctorsAsync();
            await ReloadRepsAsync();
            await LoadPeriodAsync();
            await LoadEntriesAsync();
        }

        public async Task RefreshAllAsync()
        {
            await ReloadDoctorsAsync();
            await ReloadRepsAsync();
            await LoadPeriodAsync();
            await LoadEntriesAsync();
        }

        public async Task ReloadDoctorsAsync(bool selectNewest = false)
        {
            var list = (await _doctorRepo.GetAllAsync(false)).ToList();
            Doctors.Clear();
            foreach (var d in list) Doctors.Add(d);
            if (selectNewest && Doctors.Count > 0)
                SelectedDoctor = Doctors.OrderByDescending(x => x.Id).First();
        }

        public async Task ReloadRepsAsync(int? selectId = null)
        {
            var list = (await _repRepo.GetAllAsync(false)).ToList();
            MedicalReps.Clear();
            foreach (var r in list) MedicalReps.Add(r);
            if (selectId != null)
                SelectedRep = MedicalReps.FirstOrDefault(x => x.Id == selectId);
        }

        private void ShiftPeriod(int direction)
        {
            // In week view step by a week; in month view step by a month.
            if (IsWeekView)
                SelectedDate = SelectedDate.AddDays(7 * direction);
            else
                DisplayMonth = DisplayMonth.AddMonths(direction);
        }

        private async Task OnSelectedDateChangedAsync()
        {
            await LoadEntriesAsync();
            // Re-mark week-strip selection (and refresh statuses when the week moved).
            await LoadPeriodAsync();
        }

        private async Task LoadPeriodAsync()
        {
            try
            {
                DateOnly rangeStart, rangeEnd;
                if (IsWeekView)
                {
                    (rangeStart, rangeEnd) = WeekBounds(DateOnly.FromDateTime(SelectedDate));
                }
                else
                {
                    var first = new DateOnly(DisplayMonth.Year, DisplayMonth.Month, 1);
                    var last = first.AddDays(DateTime.DaysInMonth(DisplayMonth.Year, DisplayMonth.Month) - 1);
                    // Pad by a week so calendar cells spilling into adjacent months still get badges.
                    rangeStart = first.AddDays(-7);
                    rangeEnd = last.AddDays(7);
                }

                var items = await _scheduleRepo.GetByRangeAsync(rangeStart, rangeEnd);

                var map = items
                    .GroupBy(i => i.ScheduleDate)
                    .ToDictionary(
                        g => g.Key,
                        g => g.All(x => x.IsCallDone) ? DayCallStatus.AllDone : DayCallStatus.HasPending);

                DayStatusMap = map;
                BuildWeekStrip(map);
            }
            catch (UnauthorizedAccessException ex)
            {
                NotificationHelper.ValidationErrorCustom(ex.Message);
            }
        }

        private void BuildWeekStrip(IReadOnlyDictionary<DateOnly, DayCallStatus> map)
        {
            var (weekStart, _) = WeekBounds(DateOnly.FromDateTime(SelectedDate));
            var selected = DateOnly.FromDateTime(SelectedDate);

            WeekDays.Clear();
            for (int i = 0; i < 7; i++)
            {
                var d = weekStart.AddDays(i);
                WeekDays.Add(new DayCellViewModel(SelectDate)
                {
                    Date = d,
                    DayName = d.DayOfWeek.ToString().Substring(0, 3),
                    DayNumber = d.Day.ToString(),
                    Status = map.TryGetValue(d, out var s) ? s : DayCallStatus.None,
                    IsSelected = d == selected
                });
            }
            OnPropertyChanged(nameof(WeekRangeLabel));
        }

        private async Task LoadEntriesAsync()
        {
            try
            {
                var date = DateOnly.FromDateTime(SelectedDate);
                var items = await _scheduleRepo.GetByDateAsync(date);

                Entries.Clear();
                int serial = 1;
                foreach (var item in items)
                {
                    Entries.Add(new CallScheduleRow
                    {
                        Id = item.Id,
                        Serial = serial++,
                        DoctorName = item.Doctor?.Name ?? "(deleted doctor)",
                        RepName = item.MedicalRep?.Name ?? "(deleted rep)",
                        Notes = item.Notes,
                        IsCallDone = item.IsCallDone,
                        CallDoneAt = item.CallDoneAt
                    });
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                NotificationHelper.ValidationErrorCustom(ex.Message);
            }
        }

        private void SelectDate(DateOnly date)
        {
            SelectedDate = date.ToDateTime(TimeOnly.MinValue);
        }

        private async Task SaveScheduleAsync()
        {
            if (SelectedDoctor == null)
            {
                NotificationHelper.ValidationErrorCustom("Please select a doctor for the scheduled call.");
                return;
            }
            if (SelectedRep == null)
            {
                NotificationHelper.ValidationErrorCustom("Please select a medical rep for the scheduled call.");
                return;
            }

            try
            {
                var schedule = new CallSchedule
                {
                    ScheduleDate = DateOnly.FromDateTime(SelectedDate),
                    DoctorId = SelectedDoctor.Id,
                    MedicalRepId = SelectedRep.Id,
                    Notes = string.IsNullOrWhiteSpace(NewNotes) ? null : NewNotes.Trim(),
                    IsCallDone = false,
                    CreatedByUserId = _user.UserId ?? 0,
                    CreatedAt = DateTime.Now
                };

                await _scheduleRepo.AddAsync(schedule);
                // No success popup — the new row appearing in the grid is the confirmation.

                NewNotes = null;
                SelectedDoctor = null;
                SelectedRep = null;

                await LoadEntriesAsync();
                await LoadPeriodAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                NotificationHelper.ValidationErrorCustom(ex.Message);
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("save call schedule", ex.Message);
            }
        }

        private async Task MarkDoneAsync(CallScheduleRow? row)
        {
            if (row == null || !row.CanMarkDone) return;

            row.IsMarking = true; // disables the button for the duration (anti double-submit)
            try
            {
                var ok = await _scheduleRepo.MarkCallDoneAsync(row.Id, _user.UserId ?? 0);
                if (ok)
                {
                    // No success popup — the row flipping to the green "Done" pill is the confirmation.
                    row.IsCallDone = true;
                    row.CallDoneAt = DateTime.Now;
                    await LoadPeriodAsync(); // refresh day badges (may flip to all-done tick)
                }
                else
                {
                    NotificationHelper.ShowInfo("This call was already marked done.", "Already Done");
                    await LoadEntriesAsync();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                NotificationHelper.ValidationErrorCustom(ex.Message);
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("mark call done", ex.Message);
            }
            finally
            {
                row.IsMarking = false;
            }
        }

        private static (DateOnly start, DateOnly end) WeekBounds(DateOnly date)
        {
            var firstDayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            int diff = ((int)date.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
            var start = date.AddDays(-diff);
            return (start, start.AddDays(6));
        }
    }

    /// <summary>A single row in the "entries for the selected date" grid.</summary>
    public sealed class CallScheduleRow : ViewModelBase
    {
        private bool _isCallDone;
        private DateTime? _callDoneAt;
        private bool _isMarking;

        public int Id { get; init; }
        public int Serial { get; init; }
        public string DoctorName { get; init; } = string.Empty;
        public string RepName { get; init; } = string.Empty;
        public string? Notes { get; init; }

        public bool IsCallDone
        {
            get => _isCallDone;
            set
            {
                if (SetProperty(ref _isCallDone, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(CanMarkDone));
                }
            }
        }

        public DateTime? CallDoneAt
        {
            get => _callDoneAt;
            set => SetProperty(ref _callDoneAt, value);
        }

        /// <summary>True while an async mark-done is in flight — used to disable the button.</summary>
        public bool IsMarking
        {
            get => _isMarking;
            set
            {
                if (SetProperty(ref _isMarking, value))
                    OnPropertyChanged(nameof(CanMarkDone));
            }
        }

        public string StatusText => IsCallDone ? "Done" : "Pending";
        public bool CanMarkDone => !IsCallDone && !IsMarking;
    }

    /// <summary>A single day cell in the week-view strip.</summary>
    public sealed class DayCellViewModel : ViewModelBase
    {
        private DayCallStatus _status;
        private bool _isSelected;

        public DateOnly Date { get; init; }
        public string DayName { get; init; } = string.Empty;
        public string DayNumber { get; init; } = string.Empty;

        public DayCallStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public ICommand SelectCommand { get; }

        public DayCellViewModel(Action<DateOnly> onSelect)
        {
            SelectCommand = new RelayCommand(_ => onSelect(Date));
        }
    }
}
