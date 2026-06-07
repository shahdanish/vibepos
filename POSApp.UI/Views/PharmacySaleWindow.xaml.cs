using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class PharmacySaleWindow : Window
    {
        private readonly PharmacySaleViewModel _vm;

        public PharmacySaleWindow(PharmacySaleViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;

            _vm.OpenAddPharmacyRequested += OnOpenAddPharmacy;
            _vm.OpenAddDoctorRequested += OnOpenAddDoctor;

            Loaded += async (_, _) => await _vm.LoadDataAsync();

            InputBindings.Add(new KeyBinding(new RelayCommandAdapter(() => _vm.StartNewSale()), new KeyGesture(Key.S, ModifierKeys.Control)));
        }

        private async void OnOpenAddPharmacy()
        {
            var dialog = App.Services!.GetRequiredService<PharmacyFormDialog>();
            dialog.Owner = this;
            dialog.LoadPharmacy(null);
            if (dialog.ShowDialog() == true)
                await _vm.ReloadPharmaciesAsync();
        }

        private async void OnOpenAddDoctor()
        {
            var dialog = App.Services!.GetRequiredService<DoctorFormDialog>();
            dialog.Owner = this;
            dialog.LoadDoctor(null);
            if (dialog.ShowDialog() == true)
                await _vm.ReloadDoctorsAsync();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _vm.OpenAddPharmacyRequested -= OnOpenAddPharmacy;
            _vm.OpenAddDoctorRequested -= OnOpenAddDoctor;
            base.OnClosed(e);
        }

        private sealed class RelayCommandAdapter : ICommand
        {
            private readonly Action _execute;
            public RelayCommandAdapter(Action execute) => _execute = execute;
            public event EventHandler? CanExecuteChanged;
            public bool CanExecute(object? parameter) => true;
            public void Execute(object? parameter) => _execute();
        }
    }
}
