using System.Windows;
using POSApp.Core.Entities;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class MedicalRepFormDialog : Window
    {
        private readonly MedicalRepFormViewModel _viewModel;

        public MedicalRepFormDialog(MedicalRepFormViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.CloseRequested += success => { DialogResult = success; };
        }

        public void LoadRep(MedicalRep? rep) => _viewModel.LoadRep(rep);

        /// <summary>The rep created/selected by a successful save (for caller selection).</summary>
        public MedicalRep? SavedRep => _viewModel.SavedRep;

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
