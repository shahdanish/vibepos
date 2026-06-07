using System.Windows;
using POSApp.Core.Entities;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class DoctorFormDialog : Window
    {
        private readonly DoctorFormViewModel _viewModel;

        public DoctorFormDialog(DoctorFormViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.CloseRequested += success => { DialogResult = success; };
        }

        public void LoadDoctor(Doctor? doctor) => _viewModel.LoadDoctor(doctor);

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
