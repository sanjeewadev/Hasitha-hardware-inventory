using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public enum NotificationType { Success, Error, Warning, Info }

    public class POSViewModel : ViewModelBase
    {
        private readonly IProductRepository _productRepo;
        private readonly IStockRepository _stockRepo;

        // --- POPUP STATE ---
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

        public ICommand CloseNotificationCommand { get; }

        // --- PROPERTIES ---
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterProducts(); }
        }

        private List<Product> _allProductsCache = new();
        public ObservableCollection<Product> Products { get; } = new();

        private bool _isBatchSelectorVisible;
        public bool IsBatchSelectorVisible
        {
            get => _isBatchSelectorVisible;
            set { _isBatchSelectorVisible = value; OnPropertyChanged(); }
        }

        private Product? _selectedProductForBatch;
        public ObservableCollection<StockBatch> AvailableBatches { get; } = new();
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
        public ICommand SelectProductCommand { get; }
        public ICommand AddBatchToCartCommand { get; }
        public ICommand CloseBatchSelectorCommand { get; }
        public ICommand RemoveFromCartCommand { get; }
        public ICommand CheckoutCommand { get; }

        public POSViewModel(IProductRepository pRepo, IStockRepository sRepo)
        {
            _productRepo = pRepo;
            _stockRepo = sRepo;

            SelectProductCommand = new RelayCommand<Product>(async (p) => await OpenBatchSelector(p));
            AddBatchToCartCommand = new RelayCommand<StockBatch>(AddBatchToCart);
            CloseBatchSelectorCommand = new RelayCommand(() => IsBatchSelectorVisible = false);
            CloseNotificationCommand = new RelayCommand(() => IsNotificationVisible = false);

            RemoveFromCartCommand = new RelayCommand<CartItem>((item) =>
            {
                Cart.Remove(item);
                RecalculateTotal();
            });

            CheckoutCommand = new RelayCommand(async () => await ExecuteCheckout());

            LoadProducts();
        }

        // --- METHODS ---
        private void ShowNotification(string title, string message, NotificationType type)
        {
            NotificationTitle = title;
            NotificationMessage = message;
            IsNotificationVisible = true;

            switch (type)
            {
                case NotificationType.Success:
                    NotificationColor = "#10B981"; NotificationIcon = "✅"; break;
                case NotificationType.Error:
                    NotificationColor = "#EF4444"; NotificationIcon = "⛔"; break;
                case NotificationType.Warning:
                    NotificationColor = "#F59E0B"; NotificationIcon = "⚠️"; break;
                default:
                    NotificationColor = "#3B82F6"; NotificationIcon = "ℹ️"; break;
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

            if (!validBatches.Any())
            {
                ShowNotification("Stock Error", "Product Quantity > 0, but no active batches found.", NotificationType.Error);
                return;
            }

            AvailableBatches.Clear();
            foreach (var b in validBatches) AvailableBatches.Add(b);

            IsBatchSelectorVisible = true;
        }

        private void AddBatchToCart(StockBatch batch)
        {
            if (batch == null || _selectedProductForBatch == null) return;

            var existing = Cart.FirstOrDefault(c => c.BatchId == batch.Id);
            if (existing != null)
            {
                if (existing.Quantity < batch.RemainingQuantity)
                    existing.Quantity += 1;
                else
                    ShowNotification("Limit Reached", $"Only {batch.RemainingQuantity} {_selectedProductForBatch.Unit} available.", NotificationType.Warning);
            }
            else
            {
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

                // Triggers the PriceStr sync automatically
                item.UnitPrice = item.StandardPrice;

                Cart.Add(item);
            }

            IsBatchSelectorVisible = false;
            RecalculateTotal();
        }

        private void RecalculateTotal()
        {
            GrandTotal = Cart.Sum(c => c.Total);
        }

        private async Task ExecuteCheckout()
        {
            if (Cart.Count == 0)
            {
                ShowNotification("Cart is Empty", "Scan items first.", NotificationType.Warning);
                return;
            }

            try
            {
                DateTime transactionDate = DateTime.Now;
                string receiptId = transactionDate.ToString("yyyyMMddHHmmss");

                foreach (var item in Cart)
                {
                    var allBatches = await _stockRepo.GetAllBatchesAsync();
                    var dbBatch = allBatches.FirstOrDefault(b => b.Id == item.BatchId);

                    if (dbBatch != null)
                    {
                        dbBatch.RemainingQuantity -= item.Quantity;
                        await _stockRepo.UpdateBatchAsync(dbBatch);
                    }

                    var sale = new StockMovement
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Type = StockMovementType.Out,
                        Date = transactionDate,
                        ReceiptId = receiptId,
                        UnitCost = item.CostPrice,
                        UnitPrice = item.UnitPrice,
                        StockBatchId = item.BatchId,
                        Note = $"Sale (Batch #{item.BatchId})"
                    };

                    await _stockRepo.SellStockAsync(sale);
                }

                ShowNotification("Sale Complete!", $"Transaction #{receiptId}\nTotal: Rs {GrandTotal:N2}", NotificationType.Success);

                Cart.Clear();
                RecalculateTotal();
                AmountPaid = 0;
                LoadProducts();
            }
            catch (Exception ex)
            {
                ShowNotification("Error", $"Sale failed: {ex.Message}", NotificationType.Error);
                LoadProducts();
            }
        }
    }

    // --- UPDATED CART ITEM (Live Typing Fixes) ---
    public class CartItem : ViewModelBase
    {
        private readonly Action _recalcCallback;

        public CartItem(Action recalcCallback)
        {
            _recalcCallback = recalcCallback;
            // Initialize string representation
            _quantityStr = "1";
            _quantity = 1;
        }

        public CartItem() { }

        public int ProductId { get; set; }
        public int BatchId { get; set; }
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";

        public decimal StockLimit { get; set; }
        public decimal CostPrice { get; set; }
        public string DiscountCode { get; set; } = "";
        public decimal StandardPrice { get; set; }
        public decimal MaxDiscountPercent { get; set; }

        public ICommand IncreaseQuantityCommand { get; }
        public ICommand DecreaseQuantityCommand { get; }

        // --- Quantity Logic (Live Typing) ---
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

        // --- NEW: Price Logic (Live Typing) ---
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

                    // LIVE PARSING: Convert text to decimal immediately
                    if (decimal.TryParse(value, out decimal result))
                    {
                        if (result < 0) result = 0;

                        // Update internal math without re-formatting the string yet
                        _unitPrice = result;

                        // Trigger updates
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

                // Sync UI String (formatted nicely with 2 decimal places)
                _priceStr = value.ToString("N2");

                OnPropertyChanged();
                OnPropertyChanged(nameof(PriceStr)); // Update the text box
                OnPropertyChanged(nameof(Total));

                CheckPriceSafety();
                _recalcCallback?.Invoke();
            }
        }

        private string _priceTextColor = "#059669";
        public string PriceTextColor
        {
            get => _priceTextColor;
            set { _priceTextColor = value; OnPropertyChanged(); }
        }

        private void CheckPriceSafety()
        {
            decimal minSafePrice = StandardPrice - (StandardPrice * (MaxDiscountPercent / 100m));
            if (UnitPrice < minSafePrice || UnitPrice <= 0) PriceTextColor = "#EF4444";
            else PriceTextColor = "#059669";
        }

        public decimal Total => Quantity * UnitPrice;
    }
}