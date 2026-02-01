using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums; // Ensure this exists or remove enum refs
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

        // --- POPUP / NOTIFICATION STATE ---
        private bool _isNotificationVisible;
        public bool IsNotificationVisible { get => _isNotificationVisible; set { _isNotificationVisible = value; OnPropertyChanged(); } }

        private string _notificationTitle = "";
        public string NotificationTitle { get => _notificationTitle; set { _notificationTitle = value; OnPropertyChanged(); } }

        private string _notificationMessage = "";
        public string NotificationMessage { get => _notificationMessage; set { _notificationMessage = value; OnPropertyChanged(); } }

        private string _notificationColor = "#1E293B"; // Default Dark
        public string NotificationColor { get => _notificationColor; set { _notificationColor = value; OnPropertyChanged(); } }

        private string _notificationIcon = "ℹ️";
        public string NotificationIcon { get => _notificationIcon; set { _notificationIcon = value; OnPropertyChanged(); } }

        public ICommand CloseNotificationCommand { get; }

        // --- EXISTING PROPERTIES ---
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

        // --- HELPER: SHOW CUSTOM POPUP ---
        private void ShowNotification(string title, string message, NotificationType type)
        {
            NotificationTitle = title;
            NotificationMessage = message;
            IsNotificationVisible = true;

            switch (type)
            {
                case NotificationType.Success:
                    NotificationColor = "#10B981"; // Green
                    NotificationIcon = "✅";
                    break;
                case NotificationType.Error:
                    NotificationColor = "#EF4444"; // Red
                    NotificationIcon = "⛔";
                    break;
                case NotificationType.Warning:
                    NotificationColor = "#F59E0B"; // Orange
                    NotificationIcon = "⚠️";
                    break;
                default:
                    NotificationColor = "#3B82F6"; // Blue
                    NotificationIcon = "ℹ️";
                    break;
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

            // FIX: If no batches, just show error popup, don't crash
            if (!validBatches.Any())
            {
                ShowNotification("Stock Error", "Product Quantity > 0, but no active batches found in database.", NotificationType.Error);
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
                if (existing.Quantity < batch.RemainingQuantity) existing.Quantity++;
                else ShowNotification("Limit Reached", $"You cannot add more of this batch. Only {batch.RemainingQuantity} in stock.", NotificationType.Warning);
            }
            else
            {
                var item = new CartItem(RecalculateTotal)
                {
                    ProductId = _selectedProductForBatch.Id,
                    BatchId = batch.Id,
                    Name = _selectedProductForBatch.Name,
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

            IsBatchSelectorVisible = false;
            RecalculateTotal();
        }

        private void RecalculateTotal()
        {
            GrandTotal = Cart.Sum(c => c.Total);
        }

        private async Task ExecuteCheckout()
        {
            // 1. Check Empty Cart
            if (Cart.Count == 0)
            {
                ShowNotification("Cart is Empty", "Please scan or select products before charging.", NotificationType.Warning);
                return;
            }

            // 2. Check Zero Price
            if (Cart.Any(item => item.UnitPrice <= 0))
            {
                ShowNotification("Invalid Pricing", "One or more items have a price of 0.00. Please correct this.", NotificationType.Error);
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

                // 3. SUCCESS POPUP (The Perfect One)
                ShowNotification("Sale Complete!", $"Transaction #{receiptId}\nTotal Amount: Rs {GrandTotal:N2}", NotificationType.Success);

                Cart.Clear();
                RecalculateTotal();
                AmountPaid = 0;
                LoadProducts();
            }
            catch (Exception ex)
            {
                ShowNotification("System Error", $"The sale failed: {ex.Message}", NotificationType.Error);
                LoadProducts();
            }
        }
    }

    public class CartItem : ViewModelBase
    {
        // (Keep your existing CartItem code exactly as it was, no changes needed there)
        private readonly Action _recalcCallback;
        public CartItem(Action recalcCallback) { _recalcCallback = recalcCallback; IncreaseQuantityCommand = new RelayCommand(() => Quantity++); DecreaseQuantityCommand = new RelayCommand(() => Quantity--); }
        public CartItem() { }
        public int ProductId { get; set; }
        public int BatchId { get; set; }
        public string Name { get; set; } = "";
        public int StockLimit { get; set; }
        public decimal CostPrice { get; set; }
        public string DiscountCode { get; set; } = "";
        public decimal StandardPrice { get; set; }
        public decimal MaxDiscountPercent { get; set; }
        public ICommand IncreaseQuantityCommand { get; }
        public ICommand DecreaseQuantityCommand { get; }
        private int _quantity;
        public int Quantity { get => _quantity; set { if (value > StockLimit) value = StockLimit; if (value < 1) value = 1; _quantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); _recalcCallback?.Invoke(); } }
        private decimal _unitPrice;
        public decimal UnitPrice { get => _unitPrice; set { _unitPrice = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); CheckPriceSafety(); _recalcCallback?.Invoke(); } }
        private string _priceTextColor = "#059669";
        public string PriceTextColor { get => _priceTextColor; set { _priceTextColor = value; OnPropertyChanged(); } }
        private void CheckPriceSafety() { decimal minSafePrice = StandardPrice - (StandardPrice * (MaxDiscountPercent / 100m)); if (UnitPrice < minSafePrice || UnitPrice <= 0) { PriceTextColor = "#EF4444"; } else { PriceTextColor = "#059669"; } }
        public decimal Total => Quantity * UnitPrice;
    }
}