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
    public class StockViewModel : ViewModelBase
    {
        private readonly IProductRepository _productRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IStockRepository _stockRepo;

        // --- NAVIGATION ---
        private List<Category> _allCategoriesCache = new();
        private List<Product> _allProductsCache = new();

        public ObservableCollection<Category> CategoryTree { get; } = new();
        public ObservableCollection<Product> ProductsInSelectedCategory { get; } = new();

        // 1. Category Selection
        private Category? _selectedCategory;
        public Category? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                OnPropertyChanged();
                ProductSearchText = "";
                LoadProductsForCategory();
            }
        }

        // 2. Search Inputs
        private string _categorySearchText = "";
        public string CategorySearchText
        {
            get => _categorySearchText;
            set { _categorySearchText = value; OnPropertyChanged(); FilterCategoryTree(); }
        }

        private string _productSearchText = "";
        public string ProductSearchText
        {
            get => _productSearchText;
            set { _productSearchText = value; OnPropertyChanged(); FilterProducts(); }
        }

        // 3. Product Selection
        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                _selectedProduct = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsProductSelected));

                if (_selectedProduct != null)
                {
                    // PRE-FILL Stock In
                    StockInCost = _selectedProduct.BuyingPrice;
                    StockInSellingPrice = _selectedProduct.SellingPrice;
                    StockInDiscount = (double)_selectedProduct.DiscountLimit;

                    // PRE-FILL Stock Out
                    StockOutQty = 0;
                    StockOutReason = AdjustmentReason.Correction;
                    SelectedAdjustmentBatch = null;

                    LoadBatchHistory(_selectedProduct.Id);
                }
                else
                {
                    ClearInputs();
                }
            }
        }

        public bool IsProductSelected => SelectedProduct != null;

        // --- TAB 1: STOCK IN ---
        public DateTime StockInDate { get; set; } = DateTime.Now;
        private int _stockInQty;
        public int StockInQty { get => _stockInQty; set { _stockInQty = value; OnPropertyChanged(); } }

        private decimal _stockInCost;
        public decimal StockInCost { get => _stockInCost; set { _stockInCost = value; OnPropertyChanged(); } }

        private decimal _stockInSellingPrice;
        public decimal StockInSellingPrice { get => _stockInSellingPrice; set { _stockInSellingPrice = value; OnPropertyChanged(); } }

        private double _stockInDiscount;
        public double StockInDiscount
        {
            get => _stockInDiscount;
            set
            {
                _stockInDiscount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSecretCode));
            }
        }

        public string GeneratedSecretCode => GenerateSmartDiscountCode(StockInDiscount);

        // --- TAB 2: ADJUSTMENT (OUT) ---
        public DateTime StockOutDate { get; set; } = DateTime.Now;

        private int _stockOutQty;
        public int StockOutQty { get => _stockOutQty; set { _stockOutQty = value; OnPropertyChanged(); } }

        private AdjustmentReason _stockOutReason = AdjustmentReason.Correction;
        public AdjustmentReason StockOutReason { get => _stockOutReason; set { _stockOutReason = value; OnPropertyChanged(); } }

        public IEnumerable<AdjustmentReason> AdjustmentReasons => Enum.GetValues(typeof(AdjustmentReason)).Cast<AdjustmentReason>();

        private StockBatch? _selectedAdjustmentBatch;
		public StockBatch? SelectedAdjustmentBatch
        {
            get => _selectedAdjustmentBatch;
            set { _selectedAdjustmentBatch = value; OnPropertyChanged(); }
        }

        public ObservableCollection<StockBatch> ActiveBatches { get; } = new();
        public ObservableCollection<StockBatch> BatchHistory { get; } = new();

        public ICommand StockInCommand { get; }
        public ICommand StockOutCommand { get; }

        public StockViewModel(IProductRepository pRepo, ICategoryRepository cRepo, IStockRepository sRepo)
        {
            _productRepo = pRepo;
            _categoryRepo = cRepo;
            _stockRepo = sRepo;

            StockInCommand = new RelayCommand(async () => await ExecuteStockIn());
            StockOutCommand = new RelayCommand(async () => await ExecuteStockOut());

            LoadTree();
        }

        // ... [Navigation Logic] ...
        private async void LoadTree()
        {
            var all = await _categoryRepo.GetAllAsync();
            _allCategoriesCache = all.ToList();
            FilterCategoryTree();
        }
        private void FilterCategoryTree()
        {
            CategoryTree.Clear();
            var cats = _allCategoriesCache;
            if (!string.IsNullOrWhiteSpace(CategorySearchText))
            {
                var lower = CategorySearchText.ToLower();
                cats = _allCategoriesCache.Where(c => c.Name.ToLower().Contains(lower)).ToList();
            }
            foreach (var c in cats) c.SubCategories.Clear();
            foreach (var c in cats)
            {
                if (c.ParentId != null)
                {
                    var parent = _allCategoriesCache.FirstOrDefault(p => p.Id == c.ParentId);
                    if (parent != null && (cats.Contains(parent) || string.IsNullOrWhiteSpace(CategorySearchText))) parent.SubCategories.Add(c);
                }
            }
            foreach (var c in cats.Where(x => x.ParentId == null)) CategoryTree.Add(c);
        }
        private async void LoadProductsForCategory()
        {
            _allProductsCache.Clear();
            ProductsInSelectedCategory.Clear();
            if (SelectedCategory == null) return;
            var all = await _productRepo.GetAllAsync();
            _allProductsCache = all.Where(p => p.CategoryId == SelectedCategory.Id).ToList();
            FilterProducts();
        }
        private void FilterProducts()
        {
            ProductsInSelectedCategory.Clear();
            if (string.IsNullOrWhiteSpace(ProductSearchText))
            {
                foreach (var p in _allProductsCache) ProductsInSelectedCategory.Add(p);
            }
            else
            {
                var lower = ProductSearchText.ToLower();
                var filtered = _allProductsCache.Where(p => p.Name.ToLower().Contains(lower) || p.Barcode.ToLower().Contains(lower));
                foreach (var p in filtered) ProductsInSelectedCategory.Add(p);
            }
        }

        private async void LoadBatchHistory(int productId)
        {
            BatchHistory.Clear();
            ActiveBatches.Clear();

            var batches = await _stockRepo.GetAllBatchesAsync();
            var relevant = batches.Where(b => b.ProductId == productId).OrderByDescending(b => b.ReceivedDate);

            foreach (var b in relevant)
            {
                BatchHistory.Add(b);
                if (b.RemainingQuantity > 0)
                {
                    ActiveBatches.Add(b);
                }
            }
            SelectedAdjustmentBatch = ActiveBatches.FirstOrDefault();
        }

        // --- SMART STOCK IN ---
        private async Task ExecuteStockIn()
        {
            if (SelectedProduct == null) return;

            // VALIDATION 1: Zero Quantity
            if (StockInQty <= 0)
            {
                MessageBox.Show("Please enter a quantity greater than 0.", "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // VALIDATION 2: Negative Prices
            if (StockInCost < 0 || StockInSellingPrice < 0)
            {
                MessageBox.Show("Prices cannot be negative.", "Invalid Price", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // VALIDATION 3: Profit Protection (Warning only)
            if (StockInSellingPrice < StockInCost)
            {
                if (MessageBox.Show(
                    $"Warning: Selling Price ({StockInSellingPrice}) is lower than Cost Price ({StockInCost}).\n\nYou will lose money on this batch.\n\nContinue anyway?",
                    "Profit Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
            }

            string secretCode = GenerateSmartDiscountCode(StockInDiscount);

            var batch = new StockBatch
            {
                ProductId = SelectedProduct.Id,
                InitialQuantity = StockInQty,
                RemainingQuantity = StockInQty,
                CostPrice = StockInCost,
                SellingPrice = StockInSellingPrice,
                Discount = (decimal)StockInDiscount,
                DiscountCode = secretCode,
                ReceivedDate = StockInDate
            };

            await _stockRepo.AddStockBatchAsync(batch);

            SelectedProduct.Quantity += StockInQty;
            SelectedProduct.BuyingPrice = StockInCost;
            SelectedProduct.SellingPrice = StockInSellingPrice;
            SelectedProduct.DiscountLimit = (decimal)StockInDiscount;

            await _productRepo.UpdateAsync(SelectedProduct);

            MessageBox.Show($"Stock Added Successfully!\n\nDiscount Code Generated: {secretCode}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshView();
        }

        // --- SMART ADJUSTMENT (STOCK OUT) ---
        private async Task ExecuteStockOut()
        {
            if (SelectedProduct == null) return;
            if (SelectedAdjustmentBatch == null) { MessageBox.Show("Please select a specific batch to adjust.", "Batch Required", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            // VALIDATION 1: Zero Quantity
            if (StockOutQty <= 0)
            {
                MessageBox.Show("Please enter a quantity greater than 0.", "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // VALIDATION 2: Over-removal
            if (StockOutQty > SelectedAdjustmentBatch.RemainingQuantity)
            {
                MessageBox.Show(
                    $"Cannot remove {StockOutQty} items.\n\nThe selected batch only has {SelectedAdjustmentBatch.RemainingQuantity} items remaining.",
                    "Insufficient Batch Stock",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            // VALIDATION 3: Confirmation
            var msg = $"Are you sure you want to remove {StockOutQty} items from this batch?\n\nReason: {StockOutReason}";
            if (MessageBox.Show(msg, "Confirm Adjustment", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var movement = new StockMovement
                {
                    ProductId = SelectedProduct.Id,
                    Quantity = StockOutQty,
                    Type = StockMovementType.Adjustment,
                    Date = StockOutDate,
                    Reason = StockOutReason,
                    StockBatchId = SelectedAdjustmentBatch.Id,
                    Note = $"Manual Adjustment: {StockOutReason}"
                };

                await _stockRepo.AdjustStockAsync(movement);
                MessageBox.Show("Stock Adjusted Successfully.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshView();
            }
        }

        private void RefreshView()
        {
            ClearInputs();
            if (SelectedProduct != null) LoadBatchHistory(SelectedProduct.Id);
            var currentSearch = ProductSearchText;
            LoadProductsForCategory();
            ProductSearchText = currentSearch;
        }

        private void ClearInputs()
        {
            StockInQty = 0; StockInCost = 0; StockInSellingPrice = 0; StockInDiscount = 0; StockInDate = DateTime.Now;

            StockOutQty = 0;
            StockOutDate = DateTime.Now;
            StockOutReason = AdjustmentReason.Correction;
            SelectedAdjustmentBatch = null;

            OnPropertyChanged(nameof(StockInQty)); OnPropertyChanged(nameof(StockInCost));
            OnPropertyChanged(nameof(StockInSellingPrice)); OnPropertyChanged(nameof(StockInDiscount));
            OnPropertyChanged(nameof(GeneratedSecretCode));

            OnPropertyChanged(nameof(StockOutQty));
            OnPropertyChanged(nameof(StockOutReason));
            OnPropertyChanged(nameof(SelectedAdjustmentBatch));
        }

        private string GenerateSmartDiscountCode(double discount)
        {
            var rnd = new Random();
            int firstRandom = rnd.Next(0, 10);
            int lastRandom = rnd.Next(0, 10);
            int discInt = (int)discount;
            string middlePart = discInt.ToString("000");
            return $"{firstRandom}{middlePart}{lastRandom}";
        }
    }
}