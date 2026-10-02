using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using POSApp.UI.Helpers;
using POSApp.Core.Services;

namespace POSApp.UI.Views
{
    /// <summary>
    /// The single admin screen for everything that varies between shops and countries:
    /// shop identity, currency, number/date format and regional labels.
    ///
    /// Values are stored machine-wide by <see cref="ReceiptBranding"/> and <see cref="Region"/>,
    /// and read by every print document, window title and money/date field in the app. This is
    /// what lets one build ship to any client without a developer touching the code.
    /// </summary>
    public partial class BusinessSettingsWindow : Window
    {
        /// <summary>Set while loading so the ComboBox handlers do not redraw a half-filled form.</summary>
        private bool _loading;

        /// <summary>Country codes in the order of the Country drop-down.</summary>
        private static readonly string[] RegionCodeByIndex = { RegionCodes.UnitedStates, RegionCodes.Pakistan, RegionCodes.Other };

        /// <summary>Culture of the loaded settings or the last preset applied; not edited on screen.</summary>
        private string _culture = "en-PK";

        /// <summary>Accent picked on the Appearance tab (applied live, saved with Save).</summary>
        private string _accent = ThemeManager.DefaultAccent;

        /// <summary>One editable row on the Sales Tax tab.</summary>
        public sealed class TaxCategoryRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string RateText { get; set; } = "0";
            public bool IsDefault { get; set; }
        }

        private readonly ObservableCollection<TaxCategoryRow> _taxRows = new();

        /// <summary>Null when the tab is not shown (not a US shop, or no database, e.g. in tests).</summary>
        private ITaxRepository? _taxRepository;

        /// <summary>Null when the Pharmacy tab is not shown.</summary>
        private IFrontStoreRepository? _frontStore;

        public BusinessSettingsWindow()
        {
            InitializeComponent();
            LoadAll(ReceiptBranding.Current, Region.Current);
            LoadAppearance(SettingsManager.LoadSettings());
            LoadDevices(SettingsManager.LoadSettings());
            Loaded += async (_, _) =>
            {
                await LoadSalesTaxAsync();
                await LoadPharmacyAsync();
            };

            // Appearance is previewed on the whole app; closing puts back whatever is saved,
            // so an unsaved choice never sticks around.
            Closed += (_, _) =>
            {
                var saved = SettingsManager.LoadSettings();
                ThemeManager.ApplyAccent(saved.Accent);
                ThemeManager.ApplyDensity(saved.Density);
            };
        }

        // ── Sales tax (US) ────────────────────────────────────────────────────

        private async Task LoadSalesTaxAsync()
        {
            if (!Region.IsUnitedStates || App.Services?.GetService(typeof(ITaxRepository)) is not ITaxRepository repository)
                return;

            try
            {
                await repository.EnsureUsDefaultsAsync(0m); // categories to start from; tax stays off until switched on
                var settings = await repository.GetSettingsAsync();
                _taxRows.Clear();
                foreach (var c in settings.Categories)
                    _taxRows.Add(new TaxCategoryRow
                    {
                        Id = c.Id,
                        Name = c.Name,
                        RateText = c.RatePercent.ToString("0.####", CultureInfo.InvariantCulture),
                        IsDefault = c.IsDefault
                    });
                chkTaxEnabled.IsChecked = settings.Enabled;
                gridTaxCategories.ItemsSource = _taxRows;
                _taxRepository = repository;
                SalesTaxTab.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Sales tax settings could not be loaded: " + ex.Message;
            }
        }

        private void AddTaxCategory_Click(object sender, RoutedEventArgs e)
        {
            var row = new TaxCategoryRow { Name = "New category", RateText = "0" };
            _taxRows.Add(row);
            gridTaxCategories.SelectedItem = row;
            gridTaxCategories.ScrollIntoView(row);
        }

        private void RemoveTaxCategory_Click(object sender, RoutedEventArgs e)
        {
            if (gridTaxCategories.SelectedItem is TaxCategoryRow row)
                _taxRows.Remove(row);
        }

        /// <summary>Checks and converts the Sales Tax tab, or returns an error for the cashier.</summary>
        private (List<TaxCategory>? Categories, string? Error) ReadTaxCategories()
        {
            gridTaxCategories.CommitEdit(DataGridEditingUnit.Row, true);
            var list = new List<TaxCategory>();
            var defaultTaken = false;
            foreach (var row in _taxRows)
            {
                if (string.IsNullOrWhiteSpace(row.Name))
                    return (null, "Every tax category needs a name.");
                if (!decimal.TryParse(row.RateText.Replace("%", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var rate)
                    || rate < 0 || rate > 30)
                    return (null, $"The rate for '{row.Name}' must be a percentage between 0 and 30, e.g. 8.25.");
                var isDefault = row.IsDefault && !defaultTaken;
                defaultTaken |= isDefault;
                list.Add(new TaxCategory { Id = row.Id, Name = row.Name.Trim(), RatePercent = rate, IsDefault = isDefault });
            }
            if (chkTaxEnabled.IsChecked == true && list.Count == 0)
                return (null, "Add at least one tax category before switching sales tax on.");
            return (list, null);
        }

        // ── Pharmacy (US front store) ─────────────────────────────────────────

        private async Task LoadPharmacyAsync()
        {
            if (!Region.IsUnitedStates || App.Services?.GetService(typeof(IFrontStoreRepository)) is not IFrontStoreRepository repository)
                return;
            try
            {
                var s = await repository.GetSettingsAsync();
                txtPseDaily.Text = (s.PseDailyLimitMg / 1000m).ToString("0.###", CultureInfo.InvariantCulture);
                txtPse30.Text = (s.PseThirtyDayLimitMg / 1000m).ToString("0.###", CultureInfo.InvariantCulture);
                chkBlockExpired.IsChecked = s.BlockExpired;
                _frontStore = repository;
                PharmacyTab.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Pharmacy settings could not be loaded: " + ex.Message;
            }
        }

        /// <summary>The Pharmacy tab's values, or an error for the cashier.</summary>
        private (FrontStoreSettings? Settings, string? Error) ReadPharmacy()
        {
            static decimal? Grams(string text) =>
                decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var g) && g > 0 && g <= 100 ? g : null;
            var daily = Grams(txtPseDaily.Text);
            var thirty = Grams(txtPse30.Text);
            if (daily == null || thirty == null)
                return (null, "PSE limits must be grams between 0 and 100, e.g. 3.6 and 9.");
            if (daily > thirty)
                return (null, "The daily PSE limit can't be more than the 30-day limit.");
            return (new FrontStoreSettings(daily.Value * 1000m, thirty.Value * 1000m, chkBlockExpired.IsChecked == true), null);
        }

        // ── Devices (this PC) ─────────────────────────────────────────────────

        private const string DefaultPrinterChoice = "(default printer)";

        private void LoadDevices(SettingsManager.UserSettings settings)
        {
            cboDrawerPrinter.Items.Clear();
            cboDrawerPrinter.Items.Add(DefaultPrinterChoice);
            foreach (var name in CashDrawer.InstalledPrinters())
                cboDrawerPrinter.Items.Add(name);
            if (!string.IsNullOrWhiteSpace(settings.CashDrawerPrinter) && !cboDrawerPrinter.Items.Contains(settings.CashDrawerPrinter))
                cboDrawerPrinter.Items.Add(settings.CashDrawerPrinter);
            cboDrawerPrinter.SelectedItem = string.IsNullOrWhiteSpace(settings.CashDrawerPrinter) ? DefaultPrinterChoice : settings.CashDrawerPrinter;
            cboDrawerPin.SelectedIndex = settings.CashDrawerPin5 ? 1 : 0;
            chkCashDrawer.IsChecked = settings.CashDrawerEnabled;
        }

        private string SelectedDrawerPrinter =>
            cboDrawerPrinter.SelectedItem is string s && s != DefaultPrinterChoice ? s : string.Empty;

        private void TestDrawer_Click(object sender, RoutedEventArgs e)
        {
            var error = CashDrawer.OpenOn(SelectedDrawerPrinter, cboDrawerPin.SelectedIndex == 1);
            txtStatus.Text = error == null
                ? "Sent the open-drawer signal. If the drawer stayed shut, try the other connector or check its cable."
                : "Cash drawer: " + error;
        }

        // ── Appearance ────────────────────────────────────────────────────────

        private void LoadAppearance(SettingsManager.UserSettings settings)
        {
            _accent = ThemeManager.FindAccent(settings.Accent).Name;
            AccentChoices.Children.Clear();
            foreach (var palette in ThemeManager.Accents)
            {
                var name = palette.Name;
                var choice = new RadioButton
                {
                    Style = (Style)FindResource("AccentChoice"),
                    Content = BuildAccentCard(palette),
                    IsChecked = name == _accent
                };
                AutomationProperties.SetName(choice, name);
                choice.Checked += (_, _) =>
                {
                    _accent = name;
                    ThemeManager.ApplyAccent(name);
                };
                AccentChoices.Children.Add(choice);
            }

            _loading = true;
            cboDensity.SelectedIndex =
                string.Equals(settings.Density, ThemeManager.CompactDensity, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            _loading = false;

            txtSampleTilePrice.Text = Region.Money(3.49m);
        }

        private static UIElement BuildAccentCard(AccentPalette palette)
        {
            Ellipse Dot(Color c, double size) => new()
            {
                Width = size, Height = size, Fill = new SolidColorBrush(c), Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var card = new StackPanel { Orientation = Orientation.Horizontal };
            card.Children.Add(Dot(palette.Primary, 22));
            card.Children.Add(Dot(palette.Accent, 14));
            var label = new StackPanel { Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            label.Children.Add(new TextBlock { Text = palette.Name, FontWeight = FontWeights.SemiBold });
            if (palette.Name == ThemeManager.DefaultAccent)
            {
                var note = new TextBlock { Text = "original", FontSize = 11 };
                note.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
                label.Children.Add(note);
            }
            card.Children.Add(label);
            return card;
        }

        private string SelectedDensity => cboDensity.SelectedIndex == 1 ? ThemeManager.CompactDensity : ThemeManager.DefaultDensity;

        private void Density_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            ThemeManager.ApplyDensity(SelectedDensity);
        }

        // ── Load / read ───────────────────────────────────────────────────────

        private void LoadAll(ReceiptBrandingSettings b, RegionSettingsData r)
        {
            _loading = true;

            txtStoreName.Text = b.StoreName;
            txtStoreAddress.Text = b.StoreAddress;
            txtStorePhone.Text = b.StorePhone;
            txtHeaderNote.Text = b.HeaderNote;
            txtFooterMessage.Text = b.FooterMessage;
            txtFooterNote.Text = b.FooterNote;

            var regionIndex = Array.FindIndex(RegionCodeByIndex, c => string.Equals(c, r.RegionCode, StringComparison.OrdinalIgnoreCase));
            cboRegion.SelectedIndex = regionIndex >= 0 ? regionIndex : RegionCodeByIndex.Length - 1;
            LoadRegionFields(r);

            _loading = false;
            UpdatePreview();
        }

        /// <summary>Fills the currency and regional fields (not the country picker) from settings.</summary>
        private void LoadRegionFields(RegionSettingsData r)
        {
            var wasLoading = _loading;
            _loading = true;

            _culture = r.Culture;
            txtCurrencySymbol.Text = r.CurrencySymbol;
            txtCurrencyName.Text = r.CurrencyName;
            txtCurrencyCode.Text = r.CurrencyCode;
            txtNationalIdLabel.Text = r.NationalIdLabel;
            txtStatutoryDeductionLabel.Text = r.StatutoryDeductionLabel;

            cboSymbolSide.SelectedIndex = r.SymbolSide == SymbolPosition.After ? 1 : 0;
            cboSymbolSpacing.SelectedIndex = r.SymbolSpacing ? 0 : 1;
            cboDecimals.SelectedIndex = Math.Clamp(r.DecimalPlaces, 0, 3);
            cboCompactDecimals.SelectedIndex = r.CompactDecimalPlaces == 0 ? 0 : 1;
            cboNumberFormat.SelectedIndex = (int)r.NumberFormat;
            cboNumberWords.SelectedIndex = r.NumberWords == NumberWordStyle.SouthAsian ? 1 : 0;
            cboDateStyle.SelectedIndex = (int)r.Dates;
            cboTimeStyle.SelectedIndex = (int)r.Times;

            _loading = wasLoading;
        }

        private ReceiptBrandingSettings ReadBranding() => new()
        {
            StoreName = txtStoreName.Text.Trim(),
            StoreAddress = txtStoreAddress.Text.Trim(),
            StorePhone = txtStorePhone.Text.Trim(),
            HeaderNote = txtHeaderNote.Text.Trim(),
            FooterMessage = txtFooterMessage.Text.Trim(),
            FooterNote = txtFooterNote.Text.Trim()
        };

        private RegionSettingsData ReadRegion()
        {
            var decimals = Math.Clamp(cboDecimals.SelectedIndex, 0, 3);
            return new RegionSettingsData
            {
                RegionCode = RegionCodeByIndex[Math.Clamp(cboRegion.SelectedIndex, 0, RegionCodeByIndex.Length - 1)],
                Culture = _culture,
                CurrencySymbol = txtCurrencySymbol.Text.Trim(),
                CurrencyName = txtCurrencyName.Text.Trim(),
                CurrencyCode = txtCurrencyCode.Text.Trim().ToUpperInvariant(),
                NationalIdLabel = txtNationalIdLabel.Text.Trim(),
                StatutoryDeductionLabel = txtStatutoryDeductionLabel.Text.Trim(),
                SymbolSide = cboSymbolSide.SelectedIndex == 1 ? SymbolPosition.After : SymbolPosition.Before,
                SymbolSpacing = cboSymbolSpacing.SelectedIndex != 1,
                DecimalPlaces = decimals,
                // "With decimals" follows the currency's own decimal places.
                CompactDecimalPlaces = cboCompactDecimals.SelectedIndex == 1 ? decimals : 0,
                NumberFormat = (NumberFormatStyle)Math.Clamp(cboNumberFormat.SelectedIndex, 0, 3),
                NumberWords = cboNumberWords.SelectedIndex == 1 ? NumberWordStyle.SouthAsian : NumberWordStyle.International,
                Dates = (DateStyle)Math.Clamp(cboDateStyle.SelectedIndex, 0, 2),
                Times = (TimeStyle)Math.Clamp(cboTimeStyle.SelectedIndex, 0, 2)
            };
        }

        // ── Preview ───────────────────────────────────────────────────────────

        /// <summary>
        /// Renders the sample receipt from the values on screen — which are not saved yet, so
        /// the settings are applied to <see cref="Region"/> only for the length of this method
        /// and the real ones are put straight back.
        /// </summary>
        private void UpdatePreview()
        {
            if (_loading) return;

            var b = ReadBranding();
            var draft = ReadRegion();

            if (string.IsNullOrWhiteSpace(draft.CurrencySymbol))
                draft.CurrencySymbol = "?";

            var saved = Region.Current;
            try
            {
                Region.Apply(draft);

                var headerLines = new[] { b.StoreName, b.StoreAddress, Region.Phone(b.StorePhone), b.HeaderNote }
                    .Where(l => !string.IsNullOrWhiteSpace(l));
                txtPreviewHeader.Text = string.Join(Environment.NewLine, headerLines);

                var sample = new DateTime(2026, 12, 31, 21, 30, 0);
                txtPreviewDate.Text = $"Invoice #1042      {Region.DateTimeText(sample)}";

                txtPreviewLine1.Text = $"Cooking Oil  2 × {Region.Money(450m)}   {Region.Money(900m)}";
                txtPreviewLine2.Text = $"Sugar 5kg    1 × {Region.Money(350.5m)}   {Region.Money(350.5m)}";
                txtPreviewTotal.Text = $"TOTAL   {Region.Money(1250.5m)}";
                txtPreviewWords.Text = Region.AmountInWords(1250.5m);

                var footerLines = new[] { b.FooterMessage, b.FooterNote }
                    .Where(l => !string.IsNullOrWhiteSpace(l));
                txtPreviewFooter.Text = string.Join(Environment.NewLine, footerLines);

                var samplePhone = string.IsNullOrWhiteSpace(b.StorePhone) ? "5555550123" : b.StorePhone;
                txtPreviewLabels.Text =
                    $"{Region.AmountLabelRequired}   ·   {Region.CostPriceLabel}   ·   {Region.BalanceLabel}\n" +
                    $"{Region.NationalIdLabel}   ·   {Region.StatutoryDeductionAmountLabel}   ·   " +
                    $"cash over/short {Region.MoneySigned(-75m)}\n" +
                    $"report date {Region.Date(sample)}   ·   {Region.LongDate(sample)}\n" +
                    $"price on a bill {Region.Compact(3.49m)}   ·   phone {Region.Phone(samplePhone)}";
            }
            finally
            {
                Region.Apply(saved);
            }
        }

        private void RefreshPreview_Click(object sender, RoutedEventArgs e) => UpdatePreview();

        private void Setting_Changed(object sender, SelectionChangedEventArgs e) => UpdatePreview();

        /// <summary>
        /// Picking United States or Pakistan fills in that country's formats; "Other country"
        /// leaves every field as it is so a custom setup is never wiped.
        /// </summary>
        private void Region_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;

            var preset = RegionSettingsData.PresetFor(RegionCodeByIndex[Math.Clamp(cboRegion.SelectedIndex, 0, RegionCodeByIndex.Length - 1)]);
            if (preset != null)
            {
                LoadRegionFields(preset);
                txtStatus.Text = $"Filled in the usual settings for {((ComboBoxItem)cboRegion.SelectedItem).Content}. Press Save to apply them.";
            }

            UpdatePreview();
        }

        // ── Save / defaults ───────────────────────────────────────────────────

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var branding = ReadBranding();
            var region = ReadRegion();

            FrontStoreSettings? pharmacy = null;
            if (_frontStore != null)
            {
                var (read, pharmacyError) = ReadPharmacy();
                if (pharmacyError != null)
                {
                    NotificationHelper.ValidationErrorCustom(pharmacyError);
                    return;
                }
                pharmacy = read;
            }

            List<TaxCategory>? taxCategories = null;
            if (_taxRepository != null)
            {
                var (categories, taxError) = ReadTaxCategories();
                if (taxError != null)
                {
                    NotificationHelper.ValidationErrorCustom(taxError);
                    return;
                }
                taxCategories = categories;
            }

            if (string.IsNullOrWhiteSpace(branding.StoreName))
            {
                NotificationHelper.ValidationErrorCustom("Shop Name is required — it is the main line on every receipt.");
                txtStoreName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(region.CurrencySymbol))
            {
                NotificationHelper.ValidationErrorCustom("Currency Symbol is required — it is printed on every amount.");
                txtCurrencySymbol.Focus();
                return;
            }

            if (region.CurrencyCode.Length != 3 || !region.CurrencyCode.All(char.IsAsciiLetterUpper))
            {
                NotificationHelper.ValidationErrorCustom("Currency Code must be the 3-letter ISO code, e.g. USD or PKR.");
                txtCurrencyCode.Focus();
                return;
            }

            if (!ReceiptBranding.Save(branding) || !Region.Save(region))
            {
                NotificationHelper.ValidationErrorCustom(
                    "Could not save the settings. Try running the software as Administrator.");
                return;
            }

            var accent = _accent;
            var density = SelectedDensity;
            var drawerOn = chkCashDrawer.IsChecked == true;
            var drawerPrinter = SelectedDrawerPrinter;
            var drawerPin5 = cboDrawerPin.SelectedIndex == 1;
            SettingsManager.SaveSetting(s =>
            {
                s.Accent = accent;
                s.Density = density;
                s.CashDrawerEnabled = drawerOn;
                s.CashDrawerPrinter = drawerPrinter;
                s.CashDrawerPin5 = drawerPin5;
            });

            if (_frontStore != null && pharmacy != null)
            {
                try { await _frontStore.SaveSettingsAsync(pharmacy); }
                catch (Exception ex)
                {
                    NotificationHelper.OperationFailed("save pharmacy settings", ex.Message);
                    return;
                }
            }

            if (_taxRepository != null && taxCategories != null)
            {
                try
                {
                    await _taxRepository.SaveSettingsAsync(chkTaxEnabled.IsChecked == true, taxCategories);
                    await LoadSalesTaxAsync(); // new rows get their ids
                }
                catch (Exception ex)
                {
                    NotificationHelper.OperationFailed("save sales tax", ex.Message);
                    return;
                }
            }

            UpdatePreview();
            txtStatus.Text = $"Saved at {Region.Time(DateTime.Now)}. Printouts use these immediately — " +
                             "close and reopen the other windows to refresh their on-screen labels.";
        }

        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Reset the shop details, currency and regional settings back to the built-in defaults " +
                "for the country selected on the Regional tab?",
                "Restore Defaults", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            // Only fills the fields — nothing is written until Save is pressed. "Other country"
            // has no preset of its own and falls back to the original defaults.
            var code = RegionCodeByIndex[Math.Clamp(cboRegion.SelectedIndex, 0, RegionCodeByIndex.Length - 1)];
            LoadAll(new ReceiptBrandingSettings(), RegionSettingsData.PresetFor(code) ?? new RegionSettingsData());
            txtStatus.Text = "Defaults loaded. Press Save to apply them.";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
