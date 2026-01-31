using InventorySystem.Core.Entities;
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
    public class POSViewModel : ViewModelBase
    {
        private readonly IProductRepository _productRepo;
        private readonly IStockRepository _stockRepo;

        // --- SEARCH ---
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterProducts(); }
        }

        private List<Product> _allProductsCache = new();
        public ObservableCollection<Product> Products { get; } = new();

        // --- BATCH POPUP ---
        private bool _isBatchSelectorVisible;
        public bool IsBatchSelectorVisible
        {
            get => _isBatchSelectorVisible;
            set { _isBatchSelectorVisible = value; OnPropertyChanged(); }
        }

        private Product? _selectedProductForBatch;
        public ObservableCollection<StockBatch> AvailableBatches { get; } = new();

        // --- CART ---
        public ObservableCollection<CartItem> Cart { get; } = new();

        private decimal _grandTotal;
        public decimal GrandTotal
        {
            get => _grandTotal;
            set { _grandTotal = value; OnPropertyChanged(); }
        }

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

            RemoveFromCartCommand = new RelayCommand<CartItem>((item) =>
            {
                Cart.Remove(item);
                RecalculateTotal();
            });

            CheckoutCommand = new RelayCommand(async () => await ExecuteCheckout());

            LoadProducts();
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

            var allBatches = await _stockRepo.GetAllBatchesAsync();
            var validBatches = allBatches
                .Where(b => b.ProductId == p.Id && b.RemainingQuantity > 0)
                .OrderBy(b => b.ReceivedDate)
                .ToList();

            if (!validBatches.Any())
            {
                MessageBox.Show("Error: Product shows stock, but no valid batches found.");
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
                else MessageBox.Show("Batch limit reached in cart.");
            }
            else
            {
                // Create Item and Pass Max Discount Rule
                var item = new CartItem
                {
                    ProductId = _selectedProductForBatch.Id,
                    BatchId = batch.Id,
                    Name = _selectedProductForBatch.Name,
                    StockLimit = batch.RemainingQuantity,
                    CostPrice = batch.CostPrice,

                    // The Standard Price
                    StandardPrice = batch.SellingPrice > 0 ? batch.SellingPrice : _selectedProductForBatch.SellingPrice,

                    // The Rule
                    MaxDiscountPercent = batch.Discount > 0 ? batch.Discount : _selectedProductForBatch.DiscountLimit,

                    // NEW: Pass the Secret Discount Code
                    DiscountCode = batch.DiscountCode,

                    Quantity = 1
                };

                // Set initial price (triggers validation logic inside CartItem)
                item.UnitPrice = item.StandardPrice;

                item.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(CartItem.Total)) RecalculateTotal(); };
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
            if (Cart.Count == 0) return;

            try
            {
                // CRITICAL FIX: Capture time ONCE. 
                // All items in this cart will have this EXACT timestamp.
                // This allows grouping them later as one "Sale Receipt".
                DateTime transactionDate = DateTime.Now;

                foreach (var item in Cart)
                {
                    // 1. Deduct Stock from Batch
                    var allBatches = await _stockRepo.GetAllBatchesAsync();
                    var dbBatch = allBatches.FirstOrDefault(b => b.Id == item.BatchId);

                    if (dbBatch != null)
                    {
                        dbBatch.RemainingQuantity -= item.Quantity;
                        await _stockRepo.UpdateBatchAsync(dbBatch);
                    }

                    // 2. Record Sale (WITH PRICE & COMMON DATE)
                    var sale = new StockMovement
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Type = Core.Enums.StockMovementType.Out,
                        Date = transactionDate, // <--- SHARED TIMESTAMP

                        // Financials
                        UnitCost = item.CostPrice,        // Profit tracking
                        UnitPrice = item.UnitPrice,       // Save the Selling Price

                        Note = $"Sale (Batch #{item.BatchId})"
                    };

                    await _stockRepo.SellStockAsync(sale);
                }

                MessageBox.Show($"Sale Complete!\nTotal: {GrandTotal:C}");
                Cart.Clear();
                RecalculateTotal();
                AmountPaid = 0;
                LoadProducts();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing sale: {ex.Message}");
            }
        }
    }

    // --- CART ITEM WITH VALIDATION ---
    public class CartItem : ViewModelBase
    {
        public int ProductId { get; set; }
        public int BatchId { get; set; }
        public string Name { get; set; } = "";
        public int StockLimit { get; set; }
        public decimal CostPrice { get; set; }

        // NEW: Store the discount code here so we can show it in the cart
        public string DiscountCode { get; set; } = "";

        public decimal StandardPrice { get; set; } // The base price
        public double MaxDiscountPercent { get; set; } // The rule (e.g., 10%)

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (value > StockLimit) value = StockLimit;
                if (value < 1) value = 1;
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Total));
            }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                // Calculate Minimum Allowed Price
                decimal minPrice = StandardPrice - (StandardPrice * (decimal)(MaxDiscountPercent / 100.0));

                // We allow going below minPrice for now (owner override), 
                // but you could uncomment the lines below to block it.
                // if (value < minPrice) value = minPrice; 

                _unitPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Total));
            }
        }

        public decimal Total => Quantity * UnitPrice;
    }
}