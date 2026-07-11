using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class CallScheduleWindow : Window
    {
        private readonly CallScheduleViewModel _viewModel;

        public CallScheduleWindow(CallScheduleViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.AddDoctorRequested += OnAddDoctor;
            _viewModel.AddMedicalRepRequested += OnAddMedicalRep;
        }

        // Reuse the existing Doctor form (with its case-insensitive dedupe warning).
        private void OnAddDoctor()
        {
            var dialog = App.Services!.GetRequiredService<DoctorFormDialog>();
            dialog.Owner = this;
            dialog.LoadDoctor(null);
            if (dialog.ShowDialog() == true)
                _ = _viewModel.ReloadDoctorsAsync(selectNewest: true);
        }

        private void OnAddMedicalRep()
        {
            var dialog = App.Services!.GetRequiredService<MedicalRepFormDialog>();
            dialog.Owner = this;
            dialog.LoadRep(null);
            if (dialog.ShowDialog() == true)
                _ = _viewModel.ReloadRepsAsync(dialog.SavedRep?.Id);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
