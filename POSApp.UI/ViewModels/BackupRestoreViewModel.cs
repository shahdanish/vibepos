using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using POSApp.Core.Services;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class BackupRestoreViewModel : ViewModelBase
    {
        private readonly IDatabaseBackupService _backupService;
        private readonly POSApp.Core.Interfaces.IAutoBackupService? _autoBackup;
        private bool _autoBackupEnabled;

        /// <summary>Daily automatic copy of the database (see <see cref="AutoBackupFolder"/>).</summary>
        public bool AutoBackupEnabled
        {
            get => _autoBackupEnabled;
            set
            {
                if (SetProperty(ref _autoBackupEnabled, value) && _autoBackup != null)
                    _ = SaveAutoBackupAsync(value);
            }
        }

        public string AutoBackupFolder => _autoBackup?.Folder ?? string.Empty;

        public bool HasAutoBackup => _autoBackup != null;

        private async Task SaveAutoBackupAsync(bool enabled)
        {
            try
            {
                await _autoBackup!.SetEnabledAsync(enabled);
                if (enabled)
                {
                    var made = await _autoBackup.RunIfDueAsync();
                    StatusMessage = made != null ? $"Automatic backups on. First copy: {Path.GetFileName(made)}" : "Automatic backups on.";
                }
                else
                {
                    StatusMessage = "Automatic backups off.";
                }
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("change automatic backups", ex.Message);
            }
        }
        
        private string _backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "POSApp_Backups");
        private bool _isBackupInProgress;
        private bool _isRestoreInProgress;
        private string _statusMessage = string.Empty;

        public ObservableCollection<string> AvailableBackups { get; } = new();

        public string BackupDirectory
        {
            get => _backupDirectory;
            set => SetProperty(ref _backupDirectory, value);
        }

        public bool IsBackupInProgress
        {
            get => _isBackupInProgress;
            set => SetProperty(ref _isBackupInProgress, value);
        }

        public bool IsRestoreInProgress
        {
            get => _isRestoreInProgress;
            set => SetProperty(ref _isRestoreInProgress, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private async Task LoadAutoBackupAsync()
        {
            try
            {
                _autoBackupEnabled = await _autoBackup!.IsEnabledAsync();
                OnPropertyChanged(nameof(AutoBackupEnabled));
            }
            catch
            {
                // Optional on this screen.
            }
        }

        public ICommand CreateBackupCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand RefreshBackupsCommand { get; }
        public ICommand SelectBackupDirectoryCommand { get; }

        public BackupRestoreViewModel(IDatabaseBackupService backupService, POSApp.Core.Interfaces.IAutoBackupService? autoBackup = null)
        {
            _backupService = backupService;
            _autoBackup = autoBackup;
            if (autoBackup != null)
                _ = LoadAutoBackupAsync();

            CreateBackupCommand = new RelayCommand(async _ => await CreateBackup());
            RestoreBackupCommand = new RelayCommand(async _ => await RestoreBackup());
            RefreshBackupsCommand = new RelayCommand(async _ => await LoadBackups());
            SelectBackupDirectoryCommand = new RelayCommand(_ => SelectBackupDirectory());

            _ = LoadBackups();
        }

        private async Task LoadBackups()
        {
            try
            {
                var backups = await _backupService.GetAvailableBackupsAsync(BackupDirectory);
                AvailableBackups.Clear();
                foreach (var backup in backups)
                {
                    AvailableBackups.Add(Path.GetFileName(backup));
                }

                StatusMessage = $"Found {AvailableBackups.Count} backup(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading backups: {ex.Message}";
            }
        }

        private async Task CreateBackup()
        {
            if (IsBackupInProgress) return;

            try
            {
                IsBackupInProgress = true;
                StatusMessage = "Creating backup...";

                var backupPath = await _backupService.CreateBackupAsync(BackupDirectory);
                
                StatusMessage = $"Backup created successfully: {Path.GetFileName(backupPath)}";
                NotificationHelper.ShowSuccess("Database backup created successfully!");
                
                await LoadBackups();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Backup failed: {ex.Message}";
                NotificationHelper.OperationFailed("create backup", ex.Message);
            }
            finally
            {
                IsBackupInProgress = false;
            }
        }

        private async Task RestoreBackup()
        {
            if (AvailableBackups.Count == 0)
            {
                NotificationHelper.ValidationErrorCustom("No backups available to restore.");
                return;
            }

            var selectedBackup = System.Windows.MessageBox.Show(
                "Warning: Restoring a backup will replace all current data. Continue?",
                "Confirm Restore",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (selectedBackup != System.Windows.MessageBoxResult.Yes)
                return;

            if (IsRestoreInProgress) return;

            try
            {
                IsRestoreInProgress = true;
                StatusMessage = "Restoring backup...";

                var backupPath = Path.Combine(BackupDirectory, AvailableBackups.First());
                await _backupService.RestoreFromBackupAsync(backupPath);

                StatusMessage = "Database restored successfully! Please restart the application.";
                NotificationHelper.ShowSuccess("Database restored successfully! Please restart the application.");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Restore failed: {ex.Message}";
                NotificationHelper.OperationFailed("restore backup", ex.Message);
            }
            finally
            {
                IsRestoreInProgress = false;
            }
        }

        private void SelectBackupDirectory()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            dialog.InitialDirectory = BackupDirectory;
            
            if (dialog.ShowDialog() == true)
            {
                BackupDirectory = dialog.FolderName;
                _ = LoadBackups();
            }
        }
    }
}
