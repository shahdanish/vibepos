using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public class SaleViewModel : ViewModelBase
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IFavoriteRepository? _favoriteRepository;
        private readonly ITaxRepository? _taxRepository;
        private readonly IFrontStoreRepository? _frontStoreRepository;

        // US front store: ID check, expired items, PSE logbook, FSA/HSA.
        private FrontStoreSettings _frontStore = FrontStoreSettings.Default;
        private int _idCheckedAge;
        private DateTime? _verifiedDob;
        private IReadOnlyList<PseLogEntry>? _pseEntries;
        private decimal _fsaEligibleTotal;

        // US checkout: sales tax and tenders. Pakistani tills never take this path.
        private TaxSettings _tax = TaxSettings.Off;
        private SaleTaxResult? _taxResult;
        private decimal _subtotal;
        private decimal _taxTotal;
        private List<SalePayment>? _splitPayments;
        private IReadOnlyList<SalePayment> _resolvedPayments = Array.Empty<SalePayment>();

        // Cards approved on the integrated reader for the bill on screen, not yet saved with it.
        private readonly List<SalePayment> _readerCharges = new();

        /// <summary>F1–F12 trigger the first twelve quick keys.</summary>
        public const int HotKeyCount = 12;

        private readonly List<QuickKeyTile> _allQuickKeys = new();
        private string _quickKeySearch = string.Empty;
        private bool _showQuickKeys = true;

        private string _invoiceNumber = string.Empty;
        private DateTime _saleDate = AppClock.Now;
        private bool _saleDateChosenByUser;
        private string _paymentType = "Cash";
        private string _customerName = "Cash";
        private Customer? _selectedCustomer;
        private string? _address;
        private string? _phone;
        private string? _mobileNumber;
        private decimal _preBalance;
        private string? _billNote;
        private decimal? _discountOnProducts;
        private decimal? _discountOnBill;
        private decimal _totalBill;
        private decimal? _receiveCash;
        private decimal _balance;
        private string _productSearchText = string.Empty;
        private string _barcodeInput = string.Empty;
        private Product? _selectedProduct;
        private decimal _quantity = 1;
        private decimal _unitPrice;
        private decimal _discountPercent;
        private bool _showPurchasePrice = true;
        private bool _autoPrint = false;
        private bool _useSmallBillFormat = false;

        public ObservableCollection<SaleItemViewModel> SaleItems { get; } = new();
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<Customer> Customers { get; } = new();

        /// <summary>The quick-key tiles currently shown (all of them, or those matching the filter).</summary>
        public ObservableCollection<QuickKeyTile> QuickKeys { get; } = new();

        /// <summary>Filters the quick-key tiles by name or product code.</summary>
        public string QuickKeySearch
        {
            get => _quickKeySearch;
            set
            {
                if (SetProperty(ref _quickKeySearch, value))
                    RefreshQuickKeyView();
            }
        }

        /// <summary>Whether the quick-key panel is shown beside the cart (remembered per Windows user).</summary>
        public bool ShowQuickKeys
        {
            get => _showQuickKeys;
            set
            {
                if (SetProperty(ref _showQuickKeys, value))
                {
                    SettingsManager.SaveSetting(s => s.ShowQuickKeys = value);
                    OnPropertyChanged(nameof(IsQuickKeysPanelVisible));
                }
            }
        }

        /// <summary>True when at least one product is a quick key (before filtering).</summary>
        public bool HasQuickKeys => _allQuickKeys.Count > 0;

        /// <summary>
        /// The panel appears only once the shop has quick keys, so a shop that never stars a
        /// product keeps the sale screen exactly as it was.
        /// </summary>
        public bool IsQuickKeysPanelVisible => ShowQuickKeys && HasQuickKeys;

        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set => SetProperty(ref _invoiceNumber, value);
        }

        /// <summary>
        /// The date shown on the sale screen. Setting it marks the date as the cashier's own
        /// choice; the time actually recorded comes from <see cref="SaleTime.Resolve"/>.
        /// </summary>
        public DateTime SaleDate
        {
            get => _saleDate;
            set
            {
                if (SetProperty(ref _saleDate, value))
                    _saleDateChosenByUser = true;
            }
        }

        /// <summary>US tills take cash, card, check or charge account; others keep the original list.</summary>
        public IReadOnlyList<string> PaymentTypes { get; } =
            Region.IsUnitedStates ? PaymentMethods.UnitedStates : PaymentMethods.Original;

        public string PaymentType
        {
            get => _paymentType;
            set
            {
                if (SetProperty(ref _paymentType, value))
                    OnPropertyChanged(nameof(IsCreditPayment));
            }
        }

        /// <summary>The bill goes on the customer's account ("Credit" / "Charge Account"): show the customer picker.</summary>
        public bool IsCreditPayment => PaymentMethods.IsOnAccount(_paymentType);

        // ── US checkout ──────────────────────────────────────────────────────

        /// <summary>US sale screen: tenders (and sales tax when it is switched on).</summary>
        public bool IsUsCheckout => Region.IsUnitedStates;

        /// <summary>Sales tax is charged: a US till with tax switched on in Business Settings.</summary>
        public bool TaxApplies => IsUsCheckout && _tax.Enabled;

        /// <summary>The bill before tax (after every discount).</summary>
        public decimal Subtotal
        {
            get => _subtotal;
            private set => SetProperty(ref _subtotal, value);
        }

        /// <summary>Sales tax included in <see cref="TotalBill"/>.</summary>
        public decimal TaxTotal
        {
            get => _taxTotal;
            private set => SetProperty(ref _taxTotal, value);
        }

        /// <summary>The selected customer is tax-exempt, so no tax is charged.</summary>
        public bool IsTaxExempt => TaxApplies && SelectedCustomer?.IsTaxExempt == true;

        public string TaxLabel
        {
            get
            {
                if (IsTaxExempt) return "Sales tax (exempt)";
                var rates = _taxResult?.ByRate;
                return rates is { Count: 1 } ? $"Sales tax {rates[0].RatePercent:0.###}%" : "Sales tax";
            }
        }

        /// <summary>A split payment has been entered and will be used on Save/Print.</summary>
        public bool IsSplitPayment => _splitPayments != null;

        /// <summary>"Cash $20.00 + Card $25.95" for the sale screen.</summary>
        public string SplitSummary => _splitPayments == null
            ? string.Empty
            : "Split: " + string.Join(" + ", _splitPayments.Select(p => $"{p.Method} {Region.Money(p.Amount)}"));

        // ── US front store ───────────────────────────────────────────────────

        /// <summary>
        /// Set by the window: asks the cashier to check photo ID for an age-restricted item
        /// (minimum age, product name). Returns the customer's date of birth, or null if cancelled.
        /// </summary>
        public Func<int, string, DateTime?>? RequestIdCheck { get; set; }

        /// <summary>
        /// Set by the window: takes the purchaser's details for the pseudoephedrine logbook and
        /// checks the purchase limits. Returns the logbook lines, or null if cancelled or refused.
        /// </summary>
        public Func<IReadOnlyList<PseCheckoutLine>, IReadOnlyList<PseLogEntry>?>? RequestPseLog { get; set; }

        /// <summary>
        /// Set by the window when an integrated card reader is set up: charges the amount on the
        /// reader (amount, description) and returns the approved charge, or null when declined or cancelled.
        /// </summary>
        public Func<decimal, string, CardChargeResult?>? RequestCardCharge { get; set; }

        /// <summary>
        /// Set with <see cref="RequestCardCharge"/>: refunds an approved charge that no saved sale
        /// uses (the bill changed or was abandoned). Returns true once refunded.
        /// </summary>
        public Func<string, decimal, Task<bool>>? VoidCardCharge { get; set; }

        /// <summary>A card was charged on the reader for this bill but the sale is not saved yet.</summary>
        public bool HasUnsavedCardCharges => _readerCharges.Count > 0;

        /// <summary>FSA/HSA amounts are tracked (US, the Pro front-store feature).</summary>
        public bool TracksFsa => IsUsCheckout && EditionGate.IsEnabled(AppFeature.FrontStorePharmacy);

        /// <summary>What an FSA/HSA card may pay on this bill: eligible items and their tax.</summary>
        public decimal FsaEligibleTotal
        {
            get => _fsaEligibleTotal;
            private set => SetProperty(ref _fsaEligibleTotal, value);
        }

        /// <summary>The minimum age the cashier has checked ID for on this sale (0 = none).</summary>
        public int IdCheckedAge => _idCheckedAge;

        /// <summary>
        /// US checks before an item goes in the cart: not past its expiry date, and photo ID
        /// for age-restricted items (once per sale; a later item needing an older age asks again
        /// only if the date of birth already given isn't old enough).
        /// </summary>
        private bool PassesFrontStoreChecks(Product product)
        {
            if (!IsUsCheckout) return true;

            if (_frontStore.BlockExpired && product.ExpiryDate is DateTime expiry && expiry.Date < AppClock.Now.Date)
            {
                NotificationHelper.ShowError($"'{product.ProductName}' expired on {Region.Date(expiry)} and can't be sold. Please take it off the shelf.");
                return false;
            }

            if (product.MinimumAge > _idCheckedAge)
            {
                var dob = _verifiedDob ?? RequestIdCheck?.Invoke(product.MinimumAge, product.ProductName);
                if (dob == null) return false;
                var age = AgeCheck.AgeOn(dob.Value, AppClock.Now);
                if (age < product.MinimumAge)
                {
                    NotificationHelper.ShowError($"The customer is {age}. '{product.ProductName}' can only be sold to customers {product.MinimumAge} or older.");
                    return false;
                }
                _verifiedDob = dob;
                _idCheckedAge = Math.Max(_idCheckedAge, product.MinimumAge);
            }
            return true;
        }

        /// <summary>
        /// US: a bill with pseudoephedrine needs the purchaser in the logbook (and within the
        /// limits) before it can be printed or saved.
        /// </summary>
        private bool ResolvePse()
        {
            _pseEntries = null;
            if (!IsUsCheckout) return true;

            var lines = SaleItems
                .Select(i => (Item: i, Product: Products.FirstOrDefault(p => p.ProductId == i.ProductId)))
                .Where(x => x.Product?.IsPse == true)
                .Select(x => new PseCheckoutLine(x.Item.ProductId, x.Item.ProductName, x.Item.Quantity, x.Item.Quantity * x.Product!.PseBaseMgPerPack))
                .ToList();
            if (lines.Count == 0) return true;

            var entries = RequestPseLog?.Invoke(lines);
            if (entries == null) return false;
            _pseEntries = entries;
            return true;
        }

        private decimal ComputeFsaEligible()
        {
            if (!TracksFsa || SaleItems.Count == 0) return 0;
            var eligible = SaleItems.Select(i => Products.FirstOrDefault(p => p.ProductId == i.ProductId)?.IsFsaEligible == true).ToList();
            if (!eligible.Contains(true)) return 0;

            var lines = _taxResult?.Lines ?? SaleTaxCalculator.Calculate(
                SaleItems.Select(i => new TaxLineInput(i.Total, 0m)).ToList(),
                (DiscountOnProducts ?? 0) + (DiscountOnBill ?? 0), taxExempt: false).Lines;
            return FsaEligibility.EligibleAmount(lines.Select((l, i) => new FsaLine(eligible[i], l.Taxable, l.Tax)));
        }

        /// <summary>The split entered for this bill, or null.</summary>
        public IReadOnlyList<SalePayment>? SplitPayments => _splitPayments;

        /// <summary>The tenders the last printed/saved bill was paid with (US).</summary>
        public IReadOnlyList<SalePayment> Payments => _resolvedPayments;

        /// <summary>The per-line tax of the current bill (US); null when no tax is charged.</summary>
        public SaleTaxResult? TaxResult => _taxResult;

        /// <summary>Uses the tenders from the Split payment dialog for this bill.</summary>
        public void ApplySplitPayments(IReadOnlyList<SalePayment> payments)
        {
            _splitPayments = payments.ToList();
            ReceiveCash = _splitPayments.Sum(p => p.Tendered);
            OnSplitChanged();
        }

        /// <summary>Drops a split payment (also done whenever the bill total changes).</summary>
        public void ClearSplit()
        {
            if (_splitPayments == null) return;
            _splitPayments = null;
            ReceiveCash = null;
            OnSplitChanged();
        }

        private void OnSplitChanged()
        {
            OnPropertyChanged(nameof(IsSplitPayment));
            OnPropertyChanged(nameof(SplitSummary));
        }

        private decimal RateFor(string productId) =>
            _tax.RateFor(Products.FirstOrDefault(p => p.ProductId == productId)?.TaxCategoryId);

        /// <summary>
        /// US only: works out the tenders for this bill (the split, or the single payment method)
        /// before it is printed or saved. False, with the cashier told why, when it can't be paid.
        /// </summary>
        internal bool ResolvePayments()
        {
            if (!IsUsCheckout)
            {
                _resolvedPayments = Array.Empty<SalePayment>();
                return true;
            }

            List<SalePayment> payments;
            if (_splitPayments != null)
            {
                payments = _splitPayments;
            }
            else
            {
                var (payment, error) = TenderCalculator.Single(PaymentType, TotalBill, ReceiveCash);
                if (error != null)
                {
                    NotificationHelper.ValidationErrorCustom(error);
                    return false;
                }
                payments = payment == null ? new List<SalePayment>() : new List<SalePayment> { payment };
            }

            if (payments.Any(p => PaymentMethods.IsOnAccount(p.Method)) && SelectedCustomer == null)
            {
                NotificationHelper.ValidationErrorCustom("Choose the customer whose charge account this goes on.");
                return false;
            }

            _resolvedPayments = payments;
            if (payments.Count > 0)
                ReceiveCash = payments.Sum(p => p.Tendered);
            return true;
        }

        /// <summary>
        /// US with an integrated card reader: charges each card tender on the reader, after
        /// <see cref="ResolvePayments"/>. A charge already approved for this bill (say the printer
        /// failed and the cashier tries again) is used again instead of charging twice; approved
        /// charges the bill no longer needs are refunded. False when a card was not approved.
        /// </summary>
        internal async Task<bool> ChargeCardsOnReaderAsync()
        {
            if (RequestCardCharge == null || !IsUsCheckout) return true;

            var cards = _resolvedPayments.Where(p => p.Method == PaymentMethods.Card).ToList();
            var unused = new List<SalePayment>(_readerCharges);
            foreach (var card in cards)
            {
                var earlier = card.ProcessorReference != null
                    ? unused.FirstOrDefault(c => c.ProcessorReference == card.ProcessorReference)
                    : unused.FirstOrDefault(c => c.Amount == card.Amount);
                if (earlier == null) continue;
                unused.Remove(earlier);
                CopyCardDetails(earlier, card);
            }
            await VoidReaderChargesAsync(unused);

            foreach (var card in cards.Where(c => c.ProcessorReference == null))
            {
                var result = RequestCardCharge(card.Amount, $"Sale {InvoiceNumber}");
                if (result == null) return false;
                var charged = new SalePayment
                {
                    Method = PaymentMethods.Card,
                    Amount = card.Amount,
                    Tendered = card.Amount,
                    CardBrand = result.Brand,
                    CardLast4 = result.Last4,
                    Reference = result.AuthCode,
                    ProcessorReference = result.PaymentReference
                };
                _readerCharges.Add(charged);
                CopyCardDetails(charged, card);
            }
            return true;
        }

        private static void CopyCardDetails(SalePayment from, SalePayment to)
        {
            to.CardBrand = from.CardBrand;
            to.CardLast4 = from.CardLast4;
            to.Reference = from.Reference;
            to.ProcessorReference = from.ProcessorReference;
        }

        /// <summary>Tells the cashier what happened to a reader charge (message, title, refunded). Replaced in tests.</summary>
        internal Action<string, string, bool> CardNotice { get; set; } = (message, title, refunded) =>
        {
            if (refunded) NotificationHelper.ShowInfo(message, title);
            else NotificationHelper.ShowWarning(message, title);
        };

        /// <summary>Refunds the card charges of a bill being closed without saving.</summary>
        public Task DiscardCardChargesAsync() => VoidReaderChargesAsync(_readerCharges.ToList());

        /// <summary>Refunds reader charges no sale will be saved with, telling the cashier what happened.</summary>
        private async Task VoidReaderChargesAsync(IReadOnlyList<SalePayment> charges)
        {
            foreach (var charge in charges)
            {
                _readerCharges.Remove(charge);
                bool refunded;
                try { refunded = VoidCardCharge != null && await VoidCardCharge(charge.ProcessorReference!, charge.Amount); }
                catch { refunded = false; }
                var what = $"{Region.Money(charge.Amount)} on {TenderCalculator.Describe(charge)}";
                if (refunded)
                    CardNotice($"The card charge of {what} was refunded to the card: no sale was saved with it.", "Card refunded", true);
                else
                    CardNotice($"The card charge of {what} is no longer part of a sale and could not be refunded automatically. Refund it from your Stripe dashboard.", "Card charge not refunded", false);
            }
        }

        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    if (value != null)
                    {
                        CustomerName = value.Name;
                        MobileNumber = value.CellNo ?? value.Phone;
                        PreBalance = value.CurrentBalance;
                    }
                    // A tax-exempt customer changes the US total.
                    if (TaxApplies) CalculateTotals();
                }
            }
        }

        public string CustomerName
        {
            get => _customerName;
            set => SetProperty(ref _customerName, value);
        }

        public string? Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        public string? Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        public string? MobileNumber
        {
            get => _mobileNumber;
            set => SetProperty(ref _mobileNumber, value);
        }

        public decimal PreBalance
        {
            get => _preBalance;
            set => SetProperty(ref _preBalance, value);
        }

        public string? BillNote
        {
            get => _billNote;
            set => SetProperty(ref _billNote, value);
        }

        public decimal? DiscountOnProducts
        {
            get => _discountOnProducts;
            set
            {
                if (SetProperty(ref _discountOnProducts, value))
                    CalculateTotals();
            }
        }

        public decimal? DiscountOnBill
        {
            get => _discountOnBill;
            set
            {
                if (SetProperty(ref _discountOnBill, value))
                    CalculateTotals();
            }
        }

        public decimal TotalBill
        {
            get => _totalBill;
            set => SetProperty(ref _totalBill, value);
        }

        public decimal? ReceiveCash
        {
            get => _receiveCash;
            set
            {
                if (SetProperty(ref _receiveCash, value))
                    CalculateBalance();
            }
        }

        public decimal Balance
        {
            get => _balance;
            set => SetProperty(ref _balance, value);
        }

        public string ProductSearchText
        {
            get => _productSearchText;
            set => SetProperty(ref _productSearchText, value);
        }

        /// <summary>
        /// Dedicated barcode-scan field. Submitting it (Enter / scanner) runs ScanCommand,
        /// which looks the product up and adds it — independent of the product dropdown.
        /// </summary>
        public string BarcodeInput
        {
            get => _barcodeInput;
            set => SetProperty(ref _barcodeInput, value);
        }

        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value) && value != null)
                {
                    UnitPrice = GetUnitPriceForProduct(value);

                    // Selecting from the dropdown auto-adds the item (qty 1) when Auto Add
                    // is enabled; otherwise the user adjusts qty/disc and clicks Add Item.
                    if (AutoAddItem && SaleItems != null)
                    {
                        AddItem();
                    }
                }
            }
        }

        public decimal Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set => SetProperty(ref _unitPrice, value);
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set => SetProperty(ref _discountPercent, value);
        }

        public bool ShowPurchasePrice
        {
            get => _showPurchasePrice;
            set
            {
                if (SetProperty(ref _showPurchasePrice, value))
                {
                    SettingsManager.SaveSetting(s => s.ShowPurchasePrice = value);
                }
            }
        }

        public bool AutoPrint
        {
            get => _autoPrint;
            set
            {
                if (SetProperty(ref _autoPrint, value))
                {
                    SettingsManager.SaveSetting(s => s.AutoPrint = value);
                }
            }
        }

        /// <summary>
        /// Whether picking a product from the dropdown auto-adds it to the cart on the
        /// SelectedProduct change alone. On the NORMAL Sale screen this is <c>false</c>:
        /// manual typing must NOT auto-commit the first TextSearch match — the item is
        /// only added on an EXPLICIT selection (Enter or click), handled in the view.
        /// WholeSaleViewModel overrides this to <c>true</c> to keep its existing behaviour.
        /// </summary>
        public virtual bool AutoAddItem
        {
            get => false;
            set { }
        }

        public bool UseSmallBillFormat
        {
            get => _useSmallBillFormat;
            set
            {
                if (SetProperty(ref _useSmallBillFormat, value))
                {
                    SettingsManager.SaveSetting(s => s.UseSmallBillFormat = value);
                }
            }
        }

        public decimal TotalPurchasePrice => SaleItems?.Sum(item => item.CostPrice * item.Quantity) ?? 0;
        public decimal TotalItemsDiscount => SaleItems?.Sum(item => item.EffectiveDiscountAmount) ?? 0;

        public Action? OpenQuickSaleWindow { get; set; }
        public Action? SwitchMode { get; set; }

        public virtual string ModeSwitchLabel => "⇄  Wholesale";

        public ICommand AddItemCommand { get; }
        public ICommand ScanCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand QuickSaleCommand { get; }
        public ICommand SwitchModeCommand { get; }
        public ICommand AddQuickKeyCommand { get; }
        public ICommand ToggleShowQuickKeysCommand { get; }
        public ICommand AddItemToQuickKeysCommand { get; }
        public ICommand RemoveItemFromQuickKeysCommand { get; }

        public SaleViewModel(ISaleRepository saleRepository, IProductRepository productRepository, ICustomerRepository customerRepository,
                             IFavoriteRepository? favoriteRepository = null, ITaxRepository? taxRepository = null,
                             IFrontStoreRepository? frontStoreRepository = null)
        {
            _frontStoreRepository = frontStoreRepository;
            _saleRepository = saleRepository;
            _productRepository = productRepository;
            _customerRepository = customerRepository;
            _favoriteRepository = favoriteRepository;
            _taxRepository = taxRepository;

            // Load saved settings
            var settings = SettingsManager.LoadSettings();
            _autoPrint = settings.AutoPrint;
            _useSmallBillFormat = settings.UseSmallBillFormat;
            _showPurchasePrice = settings.ShowPurchasePrice;
            _showQuickKeys = settings.ShowQuickKeys;


            SaleItems.CollectionChanged += SaleItems_CollectionChanged;

            AddItemCommand = new RelayCommand(_ => AddItem());
            ScanCommand = new RelayCommand(async _ => await ProcessBarcodeScan());
            DeleteItemCommand = new RelayCommand(DeleteItem);
            SaveCommand = new RelayCommand(async _ => await SaveSale(printAfterSave: false));
            NewCommand = new RelayCommand(_ => NewSale());
            CancelCommand = new RelayCommand(_ => Cancel());
            PrintCommand = new RelayCommand(async _ => await PrintInvoice());
            QuickSaleCommand = new RelayCommand(_ => OpenQuickSaleWindow?.Invoke());
            AddQuickKeyCommand = new RelayCommand(p => { if (p is QuickKeyTile tile) AddProductToCart(tile.Product); });
            ToggleShowQuickKeysCommand = new RelayCommand(_ => ShowQuickKeys = !ShowQuickKeys);
            AddItemToQuickKeysCommand = new RelayCommand(async p => await SetQuickKeyForCartItemAsync(p as SaleItemViewModel, true));
            RemoveItemFromQuickKeysCommand = new RelayCommand(async p => await SetQuickKeyForCartItemAsync(p as SaleItemViewModel, false));
            SwitchModeCommand = new RelayCommand(_ =>
            {
                // Retail → wholesale needs Pro in the Store Lite tier; switching back is always allowed.
                if (this is not WholeSaleViewModel && !EditionGate.Require(AppFeature.Wholesale)) return;
                SwitchMode?.Invoke();
            });

            _ = LoadData();
        }

        private void SaleItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (SaleItemViewModel item in e.OldItems)
                    item.PropertyChanged -= SaleItem_PropertyChanged;
            }
            if (e.NewItems != null)
            {
                foreach (SaleItemViewModel item in e.NewItems)
                    item.PropertyChanged += SaleItem_PropertyChanged;
            }
            CalculateTotals();
        }

        private void SaleItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SaleItemViewModel.Total) ||
                e.PropertyName == nameof(SaleItemViewModel.UnitPrice) ||
                e.PropertyName == nameof(SaleItemViewModel.Quantity) ||
                e.PropertyName == nameof(SaleItemViewModel.DiscountPercent) ||
                e.PropertyName == nameof(SaleItemViewModel.DiscountType))
            {
                CalculateTotals();
            }
        }

        private async Task LoadData()
        {
            InvoiceNumber = await _saleRepository.GetNextInvoiceNumberAsync();

            var products = await _productRepository.GetAllAsync();
            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(product);
            }

            var customers = await _customerRepository.GetAllAsync();
            Customers.Clear();
            foreach (var customer in customers)
            {
                Customers.Add(customer);
            }

            if (IsUsCheckout && _frontStoreRepository != null)
            {
                try { _frontStore = await _frontStoreRepository.GetSettingsAsync(); }
                catch { _frontStore = FrontStoreSettings.Default; }
            }

            if (IsUsCheckout && _taxRepository != null)
            {
                try { _tax = await _taxRepository.GetSettingsAsync(); }
                catch { _tax = TaxSettings.Off; } // never block the sale screen over tax settings
                OnPropertyChanged(nameof(TaxApplies));
                CalculateTotals();
            }

            await LoadQuickKeysAsync();
        }

        // ── Quick keys ─────────────────────────────────────────────────────────

        /// <summary>Re-reads the shop's quick keys (after a sale, or when they were edited elsewhere).</summary>
        public async Task LoadQuickKeysAsync()
        {
            if (_favoriteRepository == null) return;

            IReadOnlyList<Product> products;
            try
            {
                products = await _favoriteRepository.GetQuickKeyProductsAsync();
            }
            catch
            {
                return; // quick keys are a convenience; never block the sale screen over them
            }

            _allQuickKeys.Clear();
            for (var i = 0; i < products.Count; i++)
            {
                var hotKey = i < HotKeyCount ? $"F{i + 1}" : null;
                _allQuickKeys.Add(new QuickKeyTile(products[i], Region.Money(GetUnitPriceForProduct(products[i])), hotKey));
            }

            RefreshQuickKeyView();
            OnPropertyChanged(nameof(HasQuickKeys));
            OnPropertyChanged(nameof(IsQuickKeysPanelVisible));
        }

        private void RefreshQuickKeyView()
        {
            var filter = _quickKeySearch.Trim();
            QuickKeys.Clear();
            foreach (var tile in _allQuickKeys)
            {
                if (filter.Length == 0
                    || tile.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || tile.Code.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    QuickKeys.Add(tile);
            }
        }

        /// <summary>
        /// F1–F12: adds the quick key at that position (counted over all quick keys, so a key
        /// always means the same product whatever is typed in the filter). False if none.
        /// </summary>
        public bool TryAddQuickKey(int index)
        {
            if (index < 0 || index >= Math.Min(HotKeyCount, _allQuickKeys.Count)) return false;
            AddProductToCart(_allQuickKeys[index].Product);
            return true;
        }

        private async Task SetQuickKeyForCartItemAsync(SaleItemViewModel? item, bool isQuickKey)
        {
            if (item == null || _favoriteRepository == null) return;

            var product = Products.FirstOrDefault(p => p.ProductId == item.ProductId);
            var userId = SessionManager.CurrentUser?.Id;
            if (product == null || userId == null) return;

            try
            {
                await _favoriteRepository.SetQuickKeyAsync(product.Id, userId.Value, isQuickKey);
                await LoadQuickKeysAsync();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("update quick keys", ex.Message);
            }
        }

        /// <summary>
        /// Puts one unit of a product in the cart, or one more if it is already there — the
        /// rule shared by barcode scans, quick-key tiles and F-keys. False when the product
        /// cannot be sold (deleted or out of stock); the cashier is told why.
        /// </summary>
        /// <summary>Raised with the cart line that <see cref="AddProductToCart"/> added or added one more to.</summary>
        public event Action<SaleItemViewModel>? CartLineChanged;

        public bool AddProductToCart(Product product)
        {
            if (product.IsDeleted)
            {
                NotificationHelper.ShowError($"Product '{product.ProductName}' has been deleted and cannot be sold.");
                return false;
            }

            if (product.Stock <= 0)
            {
                NotificationHelper.ShowError($"Product '{product.ProductName}' is out of stock!");
                return false;
            }

            if (!PassesFrontStoreChecks(product)) return false;

            // Already in the cart: one more, instead of a duplicate row.
            var existingItem = SaleItems.FirstOrDefault(item => item.ProductId == product.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += 1;
            }
            else
            {
                // Price respects Sale vs Whole Sale
                var price = GetUnitPriceForProduct(product);
                existingItem = new SaleItemViewModel
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    Quantity = 1,
                    CostPrice = product.CostPrice,
                    UnitPrice = price,
                    DiscountPercent = 0,
                    Total = price
                };
                SaleItems.Add(existingItem);
            }

            CalculateTotals();
            CartLineChanged?.Invoke(existingItem);
            return true;
        }

        /// <summary>
        /// Resolves the selling price for a product. Overridden by WholeSaleViewModel
        /// so that barcode scanning uses the same wholesale pricing as a manual selection.
        /// </summary>
        protected virtual decimal GetUnitPriceForProduct(Product product) => product.UnitPrice;

        private async Task ProcessBarcodeScan()
        {
            var barcode = BarcodeInput?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(barcode))
                return;

            try
            {
                // Resolve by barcode first, then fall back to the Product ID so that
                // codes stored in either field auto-select exactly like a dropdown pick.
                var product = await _productRepository.GetByBarcodeAsync(barcode)
                              ?? await _productRepository.GetByProductIdAsync(barcode);

                if (product == null)
                {
                    NotificationHelper.ShowError($"No product found for code '{barcode}'.");
                    BarcodeInput = string.Empty;
                    return;
                }

                AddProductToCart(product);

                // Clear scan field, keep focus here for the next scan
                BarcodeInput = string.Empty;
            }
            catch (Exception)
            {
                // Silently ignore errors during auto-scan to prevent UI disruption
            }
        }

        private void AddItem()
        {
            // Skip validation if called from barcode scan (product is already selected)
            if (SelectedProduct == null)
            {
                NotificationHelper.ValidationErrorCustom("Please select a product and enter a valid quantity to add to the sale.");
                return;
            }

            if (Quantity <= 0)
            {
                Quantity = 1; // Default to 1 if invalid
            }

            if (!PassesFrontStoreChecks(SelectedProduct)) return;

            // If the product is already in the cart, bump its quantity instead of
            // adding a duplicate row (matches the barcode-scan behaviour).
            var existingItem = SaleItems.FirstOrDefault(i => i.ProductId == SelectedProduct.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += Quantity;
            }
            else
            {
                var total = (UnitPrice * Quantity) - ((UnitPrice * Quantity * DiscountPercent) / 100);

                var saleItem = new SaleItemViewModel
                {
                    ProductId = SelectedProduct.ProductId,
                    ProductName = SelectedProduct.ProductName,
                    Quantity = Quantity,
                    CostPrice = SelectedProduct.CostPrice, // Track cost price (encrypted in display)
                    UnitPrice = UnitPrice,
                    DiscountPercent = DiscountPercent,
                    Total = total
                };

                SaleItems.Add(saleItem);
            }

            CalculateTotals();

            // Reset entry fields synchronously
            Quantity = 1;
            UnitPrice = 0;
            DiscountPercent = 0;
            ProductSearchText = string.Empty;

            // Defer SelectedProduct reset so it runs after WPF finishes processing the
            // current ComboBox SelectionChanged event. Setting it synchronously here
            // (re-entrant setter call) prevents the ComboBox from properly clearing its
            // internal selection state, which stops subsequent product selections from firing.
            Application.Current.Dispatcher.BeginInvoke(() => { SelectedProduct = null; });
        }

        private void DeleteItem(object? parameter)
        {
            if (parameter is SaleItemViewModel item)
            {
                SaleItems.Remove(item);
                CalculateTotals();
            }
        }

        private void CalculateTotals()
        {
            if (SaleItems == null) return;

            var subtotal = SaleItems.Sum(item => item.Total);
            subtotal -= DiscountOnProducts ?? 0;
            var billTotal = subtotal - (DiscountOnBill ?? 0);

            if (TaxApplies)
            {
                _taxResult = SaleTaxCalculator.Calculate(
                    SaleItems.Select(i => new TaxLineInput(i.Total, RateFor(i.ProductId))).ToList(),
                    (DiscountOnProducts ?? 0) + (DiscountOnBill ?? 0),
                    IsTaxExempt);
                Subtotal = _taxResult.Subtotal;
                TaxTotal = _taxResult.TaxTotal;
                TotalBill = _taxResult.Total;
            }
            else
            {
                // The original calculation, unchanged.
                _taxResult = null;
                Subtotal = billTotal;
                TaxTotal = 0;
                TotalBill = billTotal;
            }
            OnPropertyChanged(nameof(TaxLabel));
            OnPropertyChanged(nameof(IsTaxExempt));
            FsaEligibleTotal = ComputeFsaEligible();

            // A split entered for a different total no longer fits the bill.
            if (_splitPayments != null && _splitPayments.Sum(p => p.Amount) != TotalBill)
                ClearSplit();

            CalculateBalance();
            OnPropertyChanged(nameof(TotalPurchasePrice));
            OnPropertyChanged(nameof(TotalItemsDiscount));
        }

        private void CalculateBalance()
        {
            Balance = (ReceiveCash ?? 0) - TotalBill;
        }

        /// <summary>
        /// Takes a fresh invoice number and stamps the real sale time just before a sale is
        /// printed or saved. The number shown while the cart was built may already have been
        /// used by the other (hidden) sale window, and the shown time can be hours old.
        /// </summary>
        private async Task PrepareForSaveAsync()
        {
            InvoiceNumber = await _saleRepository.GetNextInvoiceNumberAsync();
            _saleDate = SaleTime.Resolve(_saleDate, _saleDateChosenByUser, AppClock.Now);
            OnPropertyChanged(nameof(SaleDate));
        }

        /// <summary>Back to "now" for the next sale; the date shown is no longer the cashier's choice.</summary>
        private void ResetSaleDate()
        {
            _saleDate = AppClock.Now;
            _saleDateChosenByUser = false;
            OnPropertyChanged(nameof(SaleDate));
        }

        private async Task SaveSale(bool printAfterSave = false, bool alreadyPrepared = false)
        {
            if (!SaleItems.Any())
            {
                NotificationHelper.ValidationErrorCustom("Please add at least one item before saving the sale.");
                return;
            }

            var saved = false;
            try
            {
                if (!alreadyPrepared)
                {
                    if (!ResolvePse()) return;
                    if (!ResolvePayments()) return;
                    await PrepareForSaveAsync();
                    if (!await ChargeCardsOnReaderAsync()) return;
                }

                var sale = new Sale
                {
                    InvoiceNumber = InvoiceNumber,
                    SaleDate = SaleDate,
                    SaleType = "Sale",
                    PaymentType = IsUsCheckout && _resolvedPayments.Count > 1 ? PaymentMethods.Split : PaymentType,
                    CustomerId = SelectedCustomer?.Id,
                    CustomerName = CustomerName,
                    Address = Address,
                    Phone = Phone,
                    MobileNumber = MobileNumber,
                    PreBalance = PreBalance,
                    BillNote = BillNote,
                    DiscountOnProducts = DiscountOnProducts ?? 0,
                    DiscountOnBill = DiscountOnBill ?? 0,
                    TotalBill = TotalBill,
                    TaxTotal = TaxTotal,
                    TaxExemptNumber = IsTaxExempt ? SelectedCustomer?.TaxExemptNumber : null,
                    IsTaxExempt = IsTaxExempt,
                    IdCheckedAge = _idCheckedAge,
                    ReceiveCash = ReceiveCash ?? 0,
                    Balance = Balance,
                    AutoPrinted = AutoPrint
                };

                var lineIndex = 0;
                foreach (var item in SaleItems)
                {
                    var tax = _taxResult != null && lineIndex < _taxResult.Lines.Count ? _taxResult.Lines[lineIndex] : null;
                    lineIndex++;
                    sale.SaleItems.Add(new SaleItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        CostPrice = item.CostPrice,
                        UnitPrice = item.UnitPrice,
                        DiscountPercent = item.DiscountPercent,
                        DiscountType = item.DiscountType,
                        Total = item.Total,
                        TaxRate = tax?.RatePercent ?? 0,
                        TaxAmount = tax?.Tax ?? 0
                    });
                }

                // US: the PSE logbook lines go in with the sale.
                foreach (var entry in _pseEntries ?? Array.Empty<PseLogEntry>())
                {
                    entry.PurchaseDate = SaleDate;
                    entry.RecordedBy = SessionManager.CurrentUser?.Username ?? string.Empty;
                    sale.PseLogEntries.Add(entry);
                }

                // US: the tenders go in with the sale (one insert, so never half-saved).
                foreach (var p in _resolvedPayments)
                {
                    sale.Payments.Add(new SalePayment
                    {
                        Method = p.Method,
                        Amount = p.Amount,
                        Tendered = p.Tendered,
                        CardBrand = p.CardBrand,
                        CardLast4 = p.CardLast4,
                        Reference = p.Reference,
                        ProcessorReference = p.ProcessorReference,
                        CreatedDate = SaleDate
                    });
                }

                await _saleRepository.AddAsync(sale);
                _readerCharges.Clear();   // saved with the sale now; a later failure must not refund or charge it again
                saved = true;

                // Cash went into the till: open the drawer (when this PC has one switched on).
                var tookCash = IsUsCheckout
                    ? _resolvedPayments.Any(p => p.Method == PaymentMethods.Cash)
                    : PaymentType == "Cash";
                if (tookCash) CashDrawer.Open();

                // Decrement stock for each sold item
                foreach (var item in SaleItems)
                {
                    var product = Products.FirstOrDefault(p => p.ProductId == item.ProductId);
                    if (product != null)
                    {
                        product.Stock = Math.Max(0, product.Stock - (int)Math.Ceiling(item.Quantity));
                        await _productRepository.UpdateAsync(product);
                    }
                }

                // Update customer balance for credit sales
                if (!IsUsCheckout)
                {
                    if (PaymentType == "Credit" && SelectedCustomer != null)
                    {
                        SelectedCustomer.CurrentBalance += TotalBill;
                        SelectedCustomer.TotalPurchases += TotalBill;
                        SelectedCustomer.LastPurchaseDate = SaleDate;
                        SelectedCustomer.ModifiedDate = DateTime.Now;
                        await _customerRepository.UpdateAsync(SelectedCustomer);
                    }
                }
                else
                {
                    // US: only the charge-account part of the payment is owed by the customer.
                    var onAccount = _resolvedPayments.Where(p => PaymentMethods.IsOnAccount(p.Method)).Sum(p => p.Amount);
                    if (onAccount > 0 && SelectedCustomer != null)
                    {
                        SelectedCustomer.CurrentBalance += onAccount;
                        SelectedCustomer.TotalPurchases += TotalBill;
                        SelectedCustomer.LastPurchaseDate = SaleDate;
                        SelectedCustomer.ModifiedDate = DateTime.Now;
                        await _customerRepository.UpdateAsync(SelectedCustomer);
                    }
                }

                // Auto-print if requested (prints only — does NOT re-enter SaveSale)
                if (printAfterSave)
                {
                    DoPrint();
                }

                NewSale();
            }
            catch (Exception ex)
            {
                if (!saved)
                {
                    NotificationHelper.OperationFailed("save sale", ex.Message);
                    return;
                }

                // The sale (and any card charge) is already stored. Clear the cart so a
                // second press of Save does not charge the card again.
                CardNotice(
                    $"The sale was saved, but a step after that failed: {ex.Message}\n\nThe screen was cleared so the card is not charged again.",
                    "Sale saved",
                    true);
                NewSale();
            }
        }

        private void NewSale()
        {
            // A bill abandoned after its card was charged: give the money back.
            if (_readerCharges.Count > 0) _ = VoidReaderChargesAsync(_readerCharges.ToList());
            _idCheckedAge = 0;
            _verifiedDob = null;
            _pseEntries = null;
            _splitPayments = null;
            _resolvedPayments = Array.Empty<SalePayment>();
            OnSplitChanged();
            SaleItems.Clear();
            CustomerName = "Cash";
            SelectedCustomer = null;
            PaymentType = "Cash";
            Address = null;
            Phone = null;
            MobileNumber = null;
            PreBalance = 0;
            BillNote = null;
            DiscountOnProducts = null;
            DiscountOnBill = null;
            TotalBill = 0;
            ReceiveCash = null;
            Balance = 0;
            BarcodeInput = string.Empty;
            ResetSaleDate();
            OnPropertyChanged(nameof(TotalPurchasePrice));
            _ = LoadData();
        }

        private void Cancel()
        {
            // Close window logic will be handled in code-behind
        }

        public void LoadState(SaleViewModel source)
        {
            CustomerName = source.CustomerName;
            MobileNumber = source.MobileNumber;
            Address = source.Address;
            Phone = source.Phone;
            PaymentType = source.PaymentType;
            SelectedCustomer = source.SelectedCustomer;
            BillNote = source.BillNote;
            DiscountOnProducts = source.DiscountOnProducts;
            DiscountOnBill     = source.DiscountOnBill;
            ReceiveCash        = source.ReceiveCash;
            PreBalance         = source.PreBalance;

            SaleItems.Clear();
            foreach (var item in source.SaleItems)
            {
                SaleItems.Add(new SaleItemViewModel
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    CostPrice = item.CostPrice,
                    UnitPrice = item.UnitPrice,
                    DiscountPercent = item.DiscountPercent,
                    DiscountType = item.DiscountType,
                });
            }
            CalculateTotals();
        }

        private async Task PrintInvoice()
        {
            if (!SaleItems.Any())
            {
                NotificationHelper.ValidationErrorCustom("No items to print. Please add items to the sale first.");
                return;
            }

            // US: the PSE logbook and the tenders first, so the receipt shows how it was paid.
            if (!ResolvePse()) return;
            if (!ResolvePayments()) return;

            try
            {
                // The printed bill must carry the number and time the sale is saved with.
                await PrepareForSaveAsync();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("prepare invoice", ex.Message);
                return;
            }
            if (!await ChargeCardsOnReaderAsync()) return;

            // Print first, then save once. Pass printAfterSave: false so SaveSale
            // does not print again (which previously caused an endless popup loop).
            if (DoPrint())
            {
                await SaveSale(printAfterSave: false, alreadyPrepared: true);
            }
        }

        /// <summary>
        /// Renders and prints the current invoice. Returns true on success.
        /// Pure print operation — does NOT save the sale, to avoid recursion.
        /// </summary>
        private bool DoPrint()
        {
            try
            {
                // 1. Build the professional FlowDocument
                FlowDocument printDoc = CreateProfessionalInvoice();

                // 2. Initialize print dialog (no dialog shown)
                PrintDialog printDialog = new PrintDialog();

                // 3. Configure page size
                if (UseSmallBillFormat)
                {
                    printDoc.PageWidth = 280; // Safer for 80mm and better for scaling
                    printDoc.PageHeight = double.NaN;
                    printDoc.ColumnWidth = 260;
                    printDoc.PagePadding = new Thickness(5);
                    printDoc.FontSize = 10; // Smaller font for thermal
                }
                else
                {
                    printDoc.PageWidth = printDialog.PrintableAreaWidth;
                    printDoc.PageHeight = printDialog.PrintableAreaHeight;
                    printDoc.ColumnWidth = printDialog.PrintableAreaWidth;
                    printDoc.PagePadding = new Thickness(40);
                }

                // 4. Print the document immediately without dialog
                printDialog.PrintDocument(
                    ((IDocumentPaginatorSource)printDoc).DocumentPaginator,
                    "Invoice Printing");

                // No success popup for printing — just print silently.
                return true;
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("print invoice", ex.Message);
                return false;
            }
        }

        protected virtual string InvoiceTitle => "Bill / Invoice";

        protected virtual bool ShowCostPriceOnReceipt => false;

        /// <summary>Whether the per-item Discount column is printed on the invoice.</summary>
        protected virtual bool ShowDiscountOnReceipt => true;

        /// <summary>Whether the Date is printed in the invoice metadata block.</summary>
        protected virtual bool ShowDateOnReceipt => true;

        internal FlowDocument CreateProfessionalInvoice()
        {
            FlowDocument doc = new FlowDocument();
            doc.FontFamily = new FontFamily("Segoe UI");
            doc.FontSize = 12;
            doc.TextAlignment = TextAlignment.Left;

            // --- HEADER (per-client branding, see Admin -> Receipt Settings) ---
            doc.Blocks.Add(ReceiptBranding.BuildHeader(nameFontSize: 24, lineFontSize: 14));

            // --- BILL / INVOICE TITLE ---
            Paragraph titlePara = new Paragraph(new Bold(new Run(InvoiceTitle)))
            {
                TextAlignment = TextAlignment.Center,
                FontSize = 16,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 2, 0, 2)
            };
            doc.Blocks.Add(titlePara);

            // --- METADATA TABLE (Bill No, Date, etc.) ---
            Table metaTable = new Table() { CellSpacing = 0 };
            metaTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) });
            metaTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) });

            TableRowGroup metaGroup = new TableRowGroup();

            // Compact cell builder so the metadata block stays tight (minimal vertical space).
            TableCell MetaCell(string text, int columnSpan = 1)
            {
                var cell = new TableCell(new Paragraph(new Run(text)) { Margin = new Thickness(0) })
                {
                    Padding = new Thickness(0, 1, 0, 1)
                };
                if (columnSpan > 1) cell.ColumnSpan = columnSpan;
                return cell;
            }

            // Row 1: Bill No & Date (with time). Date is omitted on formats that hide it.
            TableRow row1 = new TableRow();
            if (ShowDateOnReceipt)
            {
                row1.Cells.Add(MetaCell($"Bill No: {InvoiceNumber}"));
                row1.Cells.Add(MetaCell($"Date: {Region.DocumentDateTime(SaleDate)}"));
            }
            else
            {
                row1.Cells.Add(MetaCell($"Bill No: {InvoiceNumber}", columnSpan: 2));
            }
            metaGroup.Rows.Add(row1);

            // Row 2: Customer
            TableRow row2 = new TableRow();
            row2.Cells.Add(MetaCell($"Customer: {CustomerName}", columnSpan: 2));
            metaGroup.Rows.Add(row2);

            // Row 3: Address & Mobile
            TableRow row3 = new TableRow();
            row3.Cells.Add(MetaCell($"Address: {Address ?? ""}"));
            row3.Cells.Add(MetaCell($"Mobile: {MobileNumber ?? ""}"));
            metaGroup.Rows.Add(row3);

            // Row 4: Salesperson
            var salesperson = SessionManager.CurrentUser?.Username;
            if (!string.IsNullOrWhiteSpace(salesperson))
            {
                TableRow spRow = new TableRow();
                spRow.Cells.Add(MetaCell($"Salesperson: {salesperson}", columnSpan: 2));
                metaGroup.Rows.Add(spRow);
            }

            // Row 5: Bill Note (only printed when provided)
            if (!string.IsNullOrWhiteSpace(BillNote))
            {
                TableRow noteRow = new TableRow();
                noteRow.Cells.Add(MetaCell($"Note: {BillNote}", columnSpan: 2));
                metaGroup.Rows.Add(noteRow);
            }

            metaTable.RowGroups.Add(metaGroup);
            doc.Blocks.Add(metaTable);


            // --- ITEMS TABLE ---
            Table itemsTable = new Table() { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 1) };
            if (UseSmallBillFormat)
            {
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.4, GridUnitType.Star) }); // S.No
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(2.0, GridUnitType.Star) }); // Product Name
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.5, GridUnitType.Star) }); // Qty
                if (ShowCostPriceOnReceipt)
                    itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.8, GridUnitType.Star) }); // Cost
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.8, GridUnitType.Star) }); // Price
                if (ShowDiscountOnReceipt)
                    itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.6, GridUnitType.Star) }); // Disc
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1.0, GridUnitType.Star) }); // Total
            }
            else
            {
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.5, GridUnitType.Star) }); // S.No
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(2.5, GridUnitType.Star) }); // Product Name
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.8, GridUnitType.Star) }); // Qty
                if (ShowCostPriceOnReceipt)
                    itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Cost
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Price
                if (ShowDiscountOnReceipt)
                    itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.8, GridUnitType.Star) }); // Disc
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Total
            }

            TableRowGroup itemsGroup = new TableRowGroup();

            // Compact, aligned cell builder for the items grid.
            TableCell ItemCell(string text, TextAlignment align)
            {
                return new TableCell(new Paragraph(new Run(text)) { TextAlignment = align, Margin = new Thickness(0) })
                {
                    BorderThickness = new Thickness(0.5),
                    BorderBrush = Brushes.Black,
                    Padding = new Thickness(2, 1, 2, 1)
                };
            }

            // Header Row
            TableRow headerRow = new TableRow() { FontWeight = FontWeights.Bold, Background = Brushes.LightGray };
            headerRow.Cells.Add(ItemCell("S.No", TextAlignment.Center));
            headerRow.Cells.Add(ItemCell("Product Name", TextAlignment.Left));
            headerRow.Cells.Add(ItemCell("Qty", TextAlignment.Center));
            if (ShowCostPriceOnReceipt)
                headerRow.Cells.Add(ItemCell("Cost", TextAlignment.Right));
            headerRow.Cells.Add(ItemCell("Price", TextAlignment.Right));
            if (ShowDiscountOnReceipt)
                headerRow.Cells.Add(ItemCell("Disc", TextAlignment.Right));
            headerRow.Cells.Add(ItemCell("Total", TextAlignment.Center));
            itemsGroup.Rows.Add(headerRow);

            // Item Rows
            int serialNo = 1;
            foreach (var item in SaleItems)
            {
                TableRow row = new TableRow();
                row.Cells.Add(ItemCell(serialNo++.ToString(), TextAlignment.Center));
                row.Cells.Add(ItemCell(item.ProductName, TextAlignment.Left));
                row.Cells.Add(ItemCell(item.Quantity.ToString(), TextAlignment.Center));
                if (ShowCostPriceOnReceipt)
                    row.Cells.Add(ItemCell(Region.Compact(item.CostPrice), TextAlignment.Right));
                row.Cells.Add(ItemCell(Region.Compact(item.UnitPrice), TextAlignment.Right));
                if (ShowDiscountOnReceipt)
                    row.Cells.Add(ItemCell(item.EffectiveDiscountAmount > 0 ? Region.Compact(item.EffectiveDiscountAmount) : "", TextAlignment.Right));
                row.Cells.Add(ItemCell(Region.Number(item.Total), TextAlignment.Right));
                itemsGroup.Rows.Add(row);
            }

            itemsTable.RowGroups.Add(itemsGroup);
            doc.Blocks.Add(itemsTable);

            // --- TOTALS SECTION ---
            // Use proportional (star) widths so the value column always fits within the
            // printable paper width. Fixed pixel widths (200 + 100) previously overflowed
            // the ~260px thermal paper, pushing the values column off the right edge and
            // printing blank totals.
            Table totalsTable = new Table() { CellSpacing = 0 };
            bool useSpacer = !UseSmallBillFormat;
            if (useSpacer)
                totalsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Left spacer (A4 only)
            totalsTable.Columns.Add(new TableColumn() { Width = new GridLength(2, GridUnitType.Star) }); // Labels
            totalsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Values

            TableRowGroup totalsGroup = new TableRowGroup();

            // Local helper keeps every row aligned and respects the format's column count.
            void AddTotalRow(string label, string value, bool bold = false, double fontSize = 12)
            {
                var row = new TableRow();
                if (useSpacer)
                    row.Cells.Add(new TableCell(new Paragraph(new Run(""))));

                var labelPara = new Paragraph(new Run(label)) { FontSize = fontSize, Margin = new Thickness(0) };
                if (bold) labelPara.FontWeight = FontWeights.Bold;
                row.Cells.Add(new TableCell(labelPara) { Padding = new Thickness(2, 1, 2, 1) });

                var valuePara = new Paragraph(new Run(value)) { TextAlignment = TextAlignment.Right, FontSize = fontSize, Margin = new Thickness(0) };
                if (bold) valuePara.FontWeight = FontWeights.Bold;
                row.Cells.Add(new TableCell(valuePara) { Padding = new Thickness(2, 1, 2, 1) });

                totalsGroup.Rows.Add(row);
            }

            if (IsUsCheckout)
            {
                // US receipt: subtotal, tax by rate, total, then how it was paid and the change.
                AddTotalRow("Subtotal", Region.Number(SaleItems.Sum(i => i.Total)));
                var billDisc = (DiscountOnBill ?? 0) + (DiscountOnProducts ?? 0);
                if (billDisc > 0)
                    AddTotalRow("Discount", "-" + Region.Number(billDisc));
                if (IsTaxExempt)
                {
                    var cert = SelectedCustomer?.TaxExemptNumber;
                    AddTotalRow("Tax exempt" + (string.IsNullOrWhiteSpace(cert) ? "" : $" #{cert}"), Region.Number(0));
                }
                else if (_taxResult != null)
                {
                    foreach (var rate in _taxResult.ByRate)
                        AddTotalRow($"Sales tax {rate.RatePercent:0.###}%", Region.Number(rate.Tax));
                }
                AddTotalRow("TOTAL", Region.Money(TotalBill), bold: true, fontSize: 14);
                if (FsaEligibleTotal > 0)
                    AddTotalRow("FSA/HSA eligible", Region.Number(FsaEligibleTotal));
                foreach (var p in _resolvedPayments)
                    AddTotalRow(TenderCalculator.Describe(p), Region.Number(p.Tendered));
                var change = TenderCalculator.Change(_resolvedPayments);
                if (change > 0)
                    AddTotalRow("Change", Region.Money(change), bold: true);
                if (TotalItemsDiscount > 0)
                    AddTotalRow("You saved", Region.Money(TotalItemsDiscount + billDisc));
                AddTotalRow("Items", SaleItems.Sum(i => i.Quantity).ToString(), bold: true);
            }
            else
            {
            if (TotalItemsDiscount > 0)
                AddTotalRow("Item Discounts", Region.Number(TotalItemsDiscount));
            var totalDisc = (DiscountOnBill ?? 0) + (DiscountOnProducts ?? 0) + TotalItemsDiscount;
            if (totalDisc > 0)
                AddTotalRow("Total Discount", Region.Number(totalDisc));
            AddTotalRow("Total Bill", Region.Number(TotalBill), bold: true);
            if (PreBalance > 0)
                AddTotalRow("Previous Balance", Region.Number(PreBalance));
            AddTotalRow("Cash Received", Region.Number(ReceiveCash ?? 0), bold: true);
            AddTotalRow("Total Items Quantity", SaleItems.Sum(i => i.Quantity).ToString(), bold: true);
            AddTotalRow("Balance Amount", Region.Number(Balance), bold: true, fontSize: 14);
            }

            totalsTable.RowGroups.Add(totalsGroup);
            doc.Blocks.Add(totalsTable);

            // US: the invoice number as a barcode, scanned at the return counter to find the sale.
            if (IsUsCheckout && !string.IsNullOrWhiteSpace(InvoiceNumber))
            {
                var barcode = InvoiceBarcode(InvoiceNumber);
                if (barcode != null) doc.Blocks.Add(barcode);
            }

            // --- FOOTER (per-client branding) ---
            doc.Blocks.Add(ReceiptBranding.BuildFooter());

            return doc;
        }

        /// <summary>A Code 128 barcode of the invoice number for the receipt, or null if it can't be drawn.</summary>
        private static BlockUIContainer? InvoiceBarcode(string invoiceNumber)
        {
            try
            {
                var png = new POSApp.Infrastructure.Services.BarcodeService().GenerateBarcode(invoiceNumber, 260, 50);
                var image = new System.Windows.Media.Imaging.BitmapImage();
                using (var stream = new System.IO.MemoryStream(png))
                {
                    image.BeginInit();
                    image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                }
                image.Freeze();
                return new BlockUIContainer(new System.Windows.Controls.Image
                {
                    Source = image, Width = 200, Height = 40, Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center
                })
                { Margin = new Thickness(0, 8, 0, 0) };
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex, "Receipt barcode");
                return null;
            }
        }
    }

    /// <summary>One quick-key tile on the sale screen.</summary>
    public sealed class QuickKeyTile
    {
        public QuickKeyTile(Product product, string priceText, string? hotKey)
        {
            Product = product;
            PriceText = priceText;
            HotKey = hotKey;
        }

        public Product Product { get; }
        public string Name => Product.ProductName;
        public string Code => Product.ProductId;

        /// <summary>The price this screen sells at (retail or wholesale), formatted for the shop.</summary>
        public string PriceText { get; }

        /// <summary>"F1".."F12" for the first twelve tiles, otherwise null.</summary>
        public string? HotKey { get; }
        public bool HasHotKey => HotKey != null;

        public bool IsOutOfStock => Product.Stock <= 0;
        public bool IsLowStock => !IsOutOfStock && Product.MinStockThreshold > 0 && Product.Stock <= Product.MinStockThreshold;

        /// <summary>Badge text: "Out", "Low · 3", or empty when stock is fine.</summary>
        public string StockBadge => IsOutOfStock ? "Out" : IsLowStock ? $"Low · {Product.Stock}" : string.Empty;
        public bool HasStockBadge => StockBadge.Length > 0;

        public string ToolTipText =>
            $"{Name} ({Code}) · {PriceText} · {Product.Stock} in stock" + (HotKey != null ? $" · {HotKey}" : string.Empty);
    }

    public sealed class SaleItemViewModel : ViewModelBase
    {
        private string _productId = string.Empty;
        private string _productName = string.Empty;
        private decimal _quantity;
        private int _bonus;
        private decimal _costPrice;
        private decimal _unitPrice;
        private decimal _discountPercent;
        private string _discountType = "%";
        private decimal _total;
        private string? _batchNo;
        private DateTime? _expiryDate;

        public string ProductId
        {
            get => _productId;
            set => SetProperty(ref _productId, value);
        }

        public string ProductName
        {
            get => _productName;
            set => SetProperty(ref _productName, value);
        }

        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                {
                    CalculateTotal();
                    OnPropertyChanged(nameof(RevenueCost));
                }
            }
        }

        public int Bonus
        {
            get => _bonus;
            set => SetProperty(ref _bonus, value);
        }

        public decimal CostPrice
        {
            get => _costPrice;
            set
            {
                if (SetProperty(ref _costPrice, value))
                    OnPropertyChanged(nameof(RevenueCost));
            }
        }

        /// <summary>Total cost basis for this line (cost price × quantity) — shown on the wholesale receipt.</summary>
        public decimal RevenueCost => CostPrice * Quantity;

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, value))
                {
                    // Auto-calculate total when unit price changes
                    CalculateTotal();
                }
            }
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (SetProperty(ref _discountPercent, value))
                {
                    CalculateTotal();
                    OnPropertyChanged(nameof(EffectiveDiscountAmount));
                    OnPropertyChanged(nameof(DiscountDisplay));
                }
            }
        }

        public string DiscountType
        {
            get => _discountType;
            set
            {
                if (SetProperty(ref _discountType, value))
                {
                    OnPropertyChanged(nameof(DiscountTypeIsPercent));
                    OnPropertyChanged(nameof(DiscountTypeIsAmount));
                    OnPropertyChanged(nameof(EffectiveDiscountAmount));
                    OnPropertyChanged(nameof(DiscountDisplay));
                    CalculateTotal();
                }
            }
        }

        public bool DiscountTypeIsPercent
        {
            get => _discountType == "%";
            set { if (value) DiscountType = "%"; }
        }

        /// <summary>
        /// Flat-amount discount rather than a percentage. Rows saved before the software
        /// was made currency-neutral stored this as "PKR", so anything that is not "%"
        /// counts as an amount.
        /// </summary>
        public bool DiscountTypeIsAmount
        {
            get => _discountType != "%";
            set { if (value) DiscountType = "AMT"; }
        }

        public decimal EffectiveDiscountAmount =>
            _discountType == "%" ? (UnitPrice * Quantity * DiscountPercent) / 100 : DiscountPercent;

        public string DiscountDisplay =>
            _discountType == "%" ? $"{_discountPercent:N0}%" : Region.MoneyWhole(_discountPercent);

        public decimal Total
        {
            get => _total;
            set => SetProperty(ref _total, value);
        }

        public string? BatchNo
        {
            get => _batchNo;
            set => SetProperty(ref _batchNo, value);
        }

        public DateTime? ExpiryDate
        {
            get => _expiryDate;
            set => SetProperty(ref _expiryDate, value);
        }

        private void CalculateTotal()
        {
            Total = _discountType == "%"
                ? (UnitPrice * Quantity) - ((UnitPrice * Quantity * DiscountPercent) / 100)
                : (UnitPrice * Quantity) - DiscountPercent;
        }
    }
}
