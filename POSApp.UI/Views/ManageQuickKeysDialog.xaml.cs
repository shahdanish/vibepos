using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    /// <summary>Reorders and removes the shop's quick keys. Returns true when something was saved.</summary>
    public partial class ManageQuickKeysDialog : Window
    {
        /// <summary>One row in the list; the hot-key label follows the row's position.</summary>
        public sealed class Row
        {
            public Row(Product product) => Product = product;
            public Product Product { get; }
            public string Name => Product.ProductName;
            public string PriceText => Region.Money(Product.UnitPrice);
            public string HotKeyLabel { get; set; } = string.Empty;
        }

        private readonly IFavoriteRepository _favorites;
        private readonly ObservableCollection<Row> _rows = new();
        private readonly HashSet<int> _removed = new();

        public ManageQuickKeysDialog()
        {
            InitializeComponent();
            // Same repository instance the sale screens use, so they never see a stale copy.
            _favorites = App.Services.GetRequiredService<IFavoriteRepository>();
            KeysList.ItemsSource = _rows;
            Loaded += async (_, _) =>
            {
                try
                {
                    foreach (var product in await _favorites.GetQuickKeyProductsAsync())
                        _rows.Add(new Row(product));
                    Relabel();
                    if (_rows.Count > 0) KeysList.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    NotificationHelper.OperationFailed("load quick keys", ex.Message);
                }
            };
        }

        private void Relabel()
        {
            for (var i = 0; i < _rows.Count; i++)
                _rows[i].HotKeyLabel = i < SaleViewModel.HotKeyCount ? $"F{i + 1}" : "·";
            KeysList.Items.Refresh();
        }

        private void Move(int delta)
        {
            var i = KeysList.SelectedIndex;
            var j = i + delta;
            if (i < 0 || j < 0 || j >= _rows.Count) return;
            _rows.Move(i, j);
            Relabel();
            KeysList.SelectedIndex = j;
            KeysList.ScrollIntoView(KeysList.SelectedItem);
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e) => Move(-1);

        private void MoveDown_Click(object sender, RoutedEventArgs e) => Move(+1);

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (KeysList.SelectedItem is not Row row) return;
            var i = KeysList.SelectedIndex;
            _removed.Add(row.Product.Id);
            _rows.Remove(row);
            Relabel();
            if (_rows.Count > 0) KeysList.SelectedIndex = Math.Min(i, _rows.Count - 1);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveButton.IsEnabled = false;
            try
            {
                var userId = SessionManager.CurrentUser?.Id ?? 0;
                foreach (var productId in _removed)
                    await _favorites.SetQuickKeyAsync(productId, userId, false);
                await _favorites.ReorderQuickKeysAsync(_rows.Select(r => r.Product.Id).ToList());
                DialogResult = true;
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("save quick keys", ex.Message);
                SaveButton.IsEnabled = true;
            }
        }
    }
}
