using System.Windows;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class SaleWindow : Window
    {
        private readonly SaleViewModel _viewModel;
        
        public SaleWindow(SaleViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;
            
            // Enable keyboard shortcuts
            KeyDown += SaleWindow_KeyDown;
        }

        private async void SaleWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Check for Ctrl+S - New Sale
            if (e.Key == System.Windows.Input.Key.S && 
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                e.Handled = true;
                _viewModel.NewCommand.Execute(null);
                return;
            }
            
            // Check for Ctrl+W - Undo Last Sale (reverse last transaction)
            if (e.Key == System.Windows.Input.Key.W && 
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                e.Handled = true;
                // Implement undo logic here if needed
                MessageBox.Show("Undo sale feature requires additional implementation.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
