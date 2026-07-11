using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class MedicalRepFormViewModel : ViewModelBase
    {
        private readonly IMedicalRepRepository _repRepository;
        private MedicalRep? _editing;

        private string _name = string.Empty;
        private string? _company;
        private string? _phone;
        private bool _isActive = true;

        public string FormTitle => _editing == null ? "Add Medical Rep" : "Edit Medical Rep";

        /// <summary>The rep saved by the last successful Save — lets callers select it in a list.</summary>
        public MedicalRep? SavedRep { get; private set; }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string? Company
        {
            get => _company;
            set => SetProperty(ref _company, value);
        }

        public string? Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool>? CloseRequested;

        public MedicalRepFormViewModel(IMedicalRepRepository repRepository)
        {
            _repRepository = repRepository;
            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        public void LoadRep(MedicalRep? rep)
        {
            _editing = rep;

            if (rep != null)
            {
                Name = rep.Name;
                Company = rep.Company;
                Phone = rep.Phone;
                IsActive = rep.IsActive;
            }
            else
            {
                Name = string.Empty;
                Company = null;
                Phone = null;
                IsActive = true;
            }

            OnPropertyChanged(nameof(FormTitle));
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                NotificationHelper.ValidationError("Medical Rep Name");
                return;
            }

            try
            {
                // Dedupe: reuse an existing rep with the same (case-insensitive) name rather
                // than blindly inserting a duplicate.
                if (_editing == null)
                {
                    var existing = await _repRepository.FindByNameAsync(Name.Trim());
                    if (existing != null)
                    {
                        if (!NotificationHelper.Confirm(
                                $"A medical rep named '{existing.Name}' already exists. Use the existing record instead of creating a duplicate?",
                                "Duplicate Medical Rep"))
                            return;

                        SavedRep = existing;
                        CloseRequested?.Invoke(true);
                        return;
                    }

                    var rep = new MedicalRep
                    {
                        Name = Name.Trim(),
                        Company = NullIfEmpty(Company),
                        Phone = NullIfEmpty(Phone),
                        IsActive = IsActive,
                    };
                    SavedRep = await _repRepository.AddAsync(rep);
                }
                else
                {
                    _editing.Name = Name.Trim();
                    _editing.Company = NullIfEmpty(Company);
                    _editing.Phone = NullIfEmpty(Phone);
                    _editing.IsActive = IsActive;
                    await _repRepository.UpdateAsync(_editing);
                    SavedRep = _editing;
                }

                CloseRequested?.Invoke(true);
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed(_editing == null ? "add medical rep" : "update medical rep", ex.Message);
            }
        }

        private static string? NullIfEmpty(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
