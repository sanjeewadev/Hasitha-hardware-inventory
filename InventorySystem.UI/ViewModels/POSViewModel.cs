using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public enum NotificationType { Success, Error, Warning, Info }

    public class POSViewModel : ViewModelBase
    {
        private readonly IProductRepository _productRepo;
        private readonly IStockRepository _stockRepo;

        // --- POPUP STATE: NOTIFICATIONS ---
        private bool _isNotificationVisible;
        public bool IsNotificationVisible { get => _isNotificationVisible; set { _isNotificationVisible = value; OnPropertyChanged(); } }
        private string _notificationTitle = "";
        public string NotificationTitle { get => _notificationTitle; set { _notificationTitle = value; OnPropertyChanged(); } }
        private string _notificationMessage = "";
        public string NotificationMessage { get => _notificationMessage; set { _notificationMessage = value; OnPropertyChanged(); } }
        private string _notificationColor = "#1E293B";
        public string NotificationColor { get => _notificationColor; set { _notificationColor = value; OnPropertyChanged(); } }
        private string _notificationIcon = "ℹ️";
        public string NotificationIcon { get => _notificationIcon; set { _notificationIcon = value; OnPropertyChanged(); } }

        // --- POPUP STATE: CREDIT CUSTOMER NAME ---
        private bool _isCustomerPopupVisible;
        public bool IsCustomerPopupVisible { get => _isCustomerPopupVisible; set { _isCustomerPopupVisible = value; OnPropertyChanged(); } }

        private string _customerNameInput = "";
        public string CustomerNameInput { get => _customerNameInput; set { _customerNameInput = value; OnPropertyChanged(); } }

        // --- DYNAMIC POPUP COLOR ---
        private string _popupHeaderColor = "#6366F1";
        public string PopupHeaderColor { get => _popupHeaderColor; set { _popupHeaderColor = value; OnPropertyChanged(); } }

        private string _popupTitleText = "Checkout";
        public string PopupTitleText { get => _popupTitleText; set { _popupTitleText = value; OnPropertyChanged(); } }

        // --- BATCH SELECTOR STATE ---
        private bool _isBatchSelectorVisible;
        public bool IsBatchSelectorVisible { get => _isBatchSelectorVisible; set { _isBatchSelectorVisible = value; OnPropertyChanged(); } }
        private Product? _selectedProductForBatch;
        public ObservableCollection<StockBatch> AvailableBatches { get; } = new();

        // --- CART & SEARCH ---
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterProducts(); }
        }
        private List<Product> _allProductsCache = new();
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<CartItem> Cart { get; } = new();

        private decimal _grandTotal;
        public decimal GrandTotal { get => _grandTotal; set { _grandTotal = value; OnPropertyChanged(); } }

        private decimal _amountPaid;
        public decimal AmountPaid
        {
            get => _amountPaid;
            set { _amountPaid = value; OnPropertyChanged(); OnPropertyChanged(nameof(ChangeAmount)); }
        }
        public decimal ChangeAmount => AmountPaid - GrandTotal;

        // --- COMMANDS ---
        public ICommand CloseNotificationCommand { get; }
        public ICommand SelectProductCommand { get; }
        public ICommand AddBatchToCartCommand { get; }
        public ICommand CloseBatchSelectorCommand { get; }
        public ICommand RemoveFromCartCommand { get; }

        public ICommand CheckoutCommand { get; }
        public ICommand PayAndPrintCommand { get; }
        public ICommand InitCreditCheckoutCommand { get; }
        public ICommand ConfirmCreditSaleCommand { get; }
        public ICommand CancelCreditPopupCommand { get; }

        public POSViewModel(IProductRepository pRepo, IStockRepository sRepo)
        {
            _productRepo = pRepo;
            _stockRepo = sRepo;

            CloseNotificationCommand = new RelayCommand(() => IsNotificationVisible = false);
            CloseBatchSelectorCommand = new RelayCommand(() => IsBatchSelectorVisible = false);

            SelectProductCommand = new RelayCommand<Product>(async (p) => await OpenBatchSelector(p));
            AddBatchToCartCommand = new RelayCommand<StockBatch>(AddBatchToCart);
            RemoveFromCartCommand = new RelayCommand<CartItem>((item) => { Cart.Remove(item); RecalculateTotal(); });

            CheckoutCommand = new RelayCommand(async () =>
            {
                PopupHeaderColor = "#009688";
                PopupTitleText = "Confirm Checkout";
                await ExecuteTransaction(isCredit: false, autoPrint: false);
            });

            PayAndPrintCommand = new RelayCommand(async () =>
            {
                PopupHeaderColor = "#6366F1";
                PopupTitleText = "Processing Payment...";
                await ExecuteTransaction(isCredit: false, autoPrint: true);
            });

            InitCreditCheckoutCommand = new RelayCommand(() =>
            {
                if (Cart.Count == 0) { ShowNotification("Empty Cart", "Add items first.", NotificationType.Warning); return; }
                PopupHeaderColor = "#EF4444";
                PopupTitleText = "Credit Sale Details";
                CustomerNameInput = "";
                IsCustomerPopupVisible = true;
            });

            CancelCreditPopupCommand = new RelayCommand(() => IsCustomerPopupVisible = false);

            ConfirmCreditSaleCommand = new RelayCommand(async () =>
            {
                if (string.IsNullOrWhiteSpace(CustomerNameInput))
                {
                    ShowNotification("Required", "Please enter Customer Name.", NotificationType.Warning);
                    return;
                }
                IsCustomerPopupVisible = false;
                await ExecuteTransaction(isCredit: true, autoPrint: false);
            });

            LoadProducts();
        }

        private async Task ExecuteTransaction(bool isCredit, bool autoPrint)
        {
            if (Cart.Count == 0) return;

            try
            {
                DateTime now = DateTime.Now;
                string receiptId = now.ToString("yyyyMMddHHmmss");

                var transaction = new SalesTransaction
                {
                    ReceiptId = receiptId,
                    TransactionDate = now,
                    TotalAmount = GrandTotal,
                    PaidAmount = isCredit ? 0 : GrandTotal,
                    IsCredit = isCredit,
                    CustomerName = isCredit ? CustomerNameInput : "Walk-in Customer",
                    Status = isCredit ? PaymentStatus.Unpaid : PaymentStatus.Paid
                };

                var stockMovements = new List<StockMovement>();

                foreach (var item in Cart)
                {
                    stockMovements.Add(new StockMovement
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Type = StockMovementType.Out,
                        Date = now,
                        UnitCost = item.CostPrice,
                        UnitPrice = item.UnitPrice,
                        StockBatchId = item.BatchId,
                        Note = isCredit ? $"Credit Sale: {transaction.CustomerName}" : "Sale"
                    });
                }

                await _stockRepo.ProcessCompleteSaleAsync(transaction, stockMovements);

                if (autoPrint)
                {
                    try
                    {
                        string printerName = "";
                        int copies = 1;
                        try
                        {
                            printerName = InventorySystem.UI.Properties.Settings.Default.PrinterName;
                            copies = InventorySystem.UI.Properties.Settings.Default.ReceiptCopies;
                        }
                        catch { }

                        string receiptText = $"H & J HARDWARE\nDate: {now}\nReceipt: {receiptId}\n----------------\n";
                        foreach (var item in Cart)
                        {
                            receiptText += $"{item.Name} x{item.Quantity}  {item.Total:N2}\n";
                        }
                        receiptText += $"----------------\nTotal: {GrandTotal:N2}\nTHANK YOU!";

                        var printService = new PrintService();
                        printService.PrintReceipt(receiptId, receiptText, printerName, copies);

                        ShowNotification("Success", "Saved & Printed!", NotificationType.Success);
                    }
                    catch (Exception printEx)
                    {
                        ShowNotification("Saved (Print Failed)", printEx.Message, NotificationType.Error);
                    }
                }
                else
                {
                    ShowNotification("Success", "Transaction Recorded.", NotificationType.Success);
                }

                Cart.Clear();
                RecalculateTotal();
                AmountPaid = 0;
                CustomerNameInput = "";
                LoadProducts();
            }
            catch (Exception ex)
            {
                ShowNotification("Transaction Failed", ex.Message, NotificationType.Error);
                LoadProducts();
            }
        }

        private void ShowNotification(string title, string message, NotificationType type)
        {
            NotificationTitle = title;
            NotificationMessage = message;
            IsNotificationVisible = true;
            switch (type)
            {
                case NotificationType.Success: NotificationColor = "#10B981"; NotificationIcon = "✅"; break;
                case NotificationType.Error: NotificationColor = "#EF4444"; NotificationIcon = "⛔"; break;
                case NotificationType.Warning: NotificationColor = "#F59E0B"; NotificationIcon = "⚠️"; break;
                default: NotificationColor = "#3B82F6"; NotificationIcon = "ℹ️"; break;
            }
        }

        private async void LoadProducts()
        {
            var all = await _productRepo.GetAllAsync();
            _allProductsCache = all.Where(p => p.Quantity > 0).ToList();
            FilterProducts();
        }

        private void FilterProducts()
        {
            Products.Clear();
            var query = _allProductsCache.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lower = SearchText.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(lower) || p.Barcode.ToLower().Contains(lower));
            }
            foreach (var p in query) Products.Add(p);
        }

        private async Task OpenBatchSelector(Product p)
        {
            if (p == null) return;
            _selectedProductForBatch = p;
            var allBatches = await _stockRepo.GetActiveBatchesAsync();
            var validBatches = allBatches.Where(b => b.ProductId == p.Id).OrderBy(b => b.ReceivedDate).ToList();

            if (!validBatches.Any()) { ShowNotification("Stock Error", "No active batches found.", NotificationType.Error); return; }

            AvailableBatches.Clear();
            foreach (var b in validBatches) AvailableBatches.Add(b);
            IsBatchSelectorVisible = true;
        }

        // --- UPDATED: Click-to-Increment Logic (Keeps Popup Open) ---
        private void AddBatchToCart(StockBatch batch)
        {
            if (batch == null || _selectedProductForBatch == null) return;

            // 1. Check if item already exists in cart
            var existing = Cart.FirstOrDefault(c => c.BatchId == batch.Id);

            if (existing != null)
            {
                // 2. Increment logic with safety check
                if (existing.Quantity < batch.RemainingQuantity)
                {
                    existing.Quantity += 1; // Increment
                }
                else
                {
                    ShowNotification("Limit Reached", $"Only {batch.RemainingQuantity} available.", NotificationType.Warning);
                }
            }
            else
            {
                // 3. Add new item
                var item = new CartItem(RecalculateTotal)
                {
                    ProductId = _selectedProductForBatch.Id,
                    BatchId = batch.Id,
                    Name = _selectedProductForBatch.Name,
                    Unit = _selectedProductForBatch.Unit,
                    StockLimit = batch.RemainingQuantity,
                    CostPrice = batch.CostPrice,
                    StandardPrice = batch.SellingPrice > 0 ? batch.SellingPrice : _selectedProductForBatch.SellingPrice,
                    MaxDiscountPercent = batch.Discount > 0 ? batch.Discount : _selectedProductForBatch.DiscountLimit,
                    DiscountCode = batch.DiscountCode,
                    Quantity = 1
                };
                item.UnitPrice = item.StandardPrice;
                Cart.Add(item);
            }

            // --- CHANGE HERE: Popup logic removed so it stays open ---
            // IsBatchSelectorVisible = false; // <--- REMOVED THIS LINE

            RecalculateTotal();
        }

        private void RecalculateTotal() { GrandTotal = Cart.Sum(c => c.Total); }
    }

    // --- CART ITEM (Same as before) ---
    public class CartItem : ViewModelBase
    {
        private readonly Action? _recalcCallback;

        public CartItem(Action recalcCallback)
        {
            _recalcCallback = recalcCallback;
            _quantityStr = "1";
            _quantity = 1;
            ApplyMaxDiscountCommand = new RelayCommand(ApplyMaxDiscount);
        }

        public CartItem()
        {
            _quantityStr = "1";
            _quantity = 1;
            ApplyMaxDiscountCommand = new RelayCommand(ApplyMaxDiscount);
        }

        public int ProductId { get; set; }
        public int BatchId { get; set; }
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";
        public decimal StockLimit { get; set; }
        public decimal CostPrice { get; set; }
        public string DiscountCode { get; set; } = "";
        public decimal StandardPrice { get; set; }
        public decimal MaxDiscountPercent { get; set; }

        public ICommand? IncreaseQuantityCommand { get; set; }
        public ICommand? DecreaseQuantityCommand { get; set; }
        public ICommand ApplyMaxDiscountCommand { get; }

        private string _quantityStr = "";
        public string QuantityStr
        {
            get => _quantityStr;
            set
            {
                if (_quantityStr != value)
                {
                    _quantityStr = value;
                    OnPropertyChanged();
                    if (decimal.TryParse(value, out decimal result))
                    {
                        if (result > StockLimit) result = StockLimit;
                        if (result < 0) result = 0;
                        _quantity = result;
                        OnPropertyChanged(nameof(Total));
                        _recalcCallback?.Invoke();
                    }
                }
            }
        }

        private decimal _quantity;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                _quantityStr = value.ToString("0.###");
                OnPropertyChanged();
                OnPropertyChanged(nameof(QuantityStr));
                OnPropertyChanged(nameof(Total));
                _recalcCallback?.Invoke();
            }
        }

        private string _priceStr = "";
        public string PriceStr
        {
            get => _priceStr;
            set
            {
                if (_priceStr != value)
                {
                    _priceStr = value;
                    OnPropertyChanged();
                    if (decimal.TryParse(value, out decimal result))
                    {
                        if (result < 0) result = 0;
                        _unitPrice = result;
                        OnPropertyChanged(nameof(Total));
                        CheckPriceSafety();
                        _recalcCallback?.Invoke();
                    }
                }
            }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                _unitPrice = value;
                _priceStr = value.ToString("N2");
                OnPropertyChanged();
                OnPropertyChanged(nameof(PriceStr));
                OnPropertyChanged(nameof(Total));
                CheckPriceSafety();
                _recalcCallback?.Invoke();
            }
        }

        private string _priceTextColor = "#059669";
        public string PriceTextColor { get => _priceTextColor; set { _priceTextColor = value; OnPropertyChanged(); } }

        private void ApplyMaxDiscount()
        {
            if (MaxDiscountPercent <= 0) return;
            decimal discountAmount = StandardPrice * (MaxDiscountPercent / 100m);
            decimal minPrice = StandardPrice - discountAmount;
            if (minPrice < 0) minPrice = 0;
            UnitPrice = minPrice;
        }

        private void CheckPriceSafety()
        {
            decimal minSafePrice = StandardPrice - (StandardPrice * (MaxDiscountPercent / 100m));
            if (UnitPrice < minSafePrice || UnitPrice <= 0) PriceTextColor = "#EF4444"; else PriceTextColor = "#059669";
        }
        public decimal Total => Quantity * UnitPrice;
    }
}