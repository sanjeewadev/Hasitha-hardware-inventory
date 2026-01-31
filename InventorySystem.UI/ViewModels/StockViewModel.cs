using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums; // Ensure you have this or remove 'StockMovementType' usage if using strings
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

        // --- LEFT PANEL: NAVIGATION ---
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

        // 3. Product Selection & Safety Lock
        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                _selectedProduct = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsProductSelected)); // Unlocks the UI

                if (_selectedProduct != null)
                {
                    // PRE-FILL Stock In
                    StockInCost = _selectedProduct.BuyingPrice;
                    StockInSellingPrice = _selectedProduct.SellingPrice;
                    StockInDiscount = _selectedProduct.DiscountLimit;

                    // PRE-FILL Stock Out Defaults
                    StockOutQty = 0;
                    StockOutReason = "Correction"; // Set Default to Correction

                    LoadBatchHistory(_selectedProduct.Id);
                }
                else
                {
                    ClearInputs();
                }
            }
        }

        // Helper for UI Enabling
        public bool IsProductSelected => SelectedProduct != null;

        // --- TAB 1: STOCK IN INPUTS ---
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
                OnPropertyChanged(nameof(GeneratedSecretCode)); // Update code when discount changes
            }
        }

        // The Display Property for the UI
        public string GeneratedSecretCode => GenerateSmartDiscountCode(StockInDiscount);

        // --- TAB 2: STOCK OUT INPUTS ---
        public DateTime StockOutDate { get; set; } = DateTime.Now;

        private int _stockOutQty;
        public int StockOutQty { get => _stockOutQty; set { _stockOutQty = value; OnPropertyChanged(); } }

        private string _stockOutReason = "Correction"; // Default
        public string StockOutReason { get => _stockOutReason; set { _stockOutReason = value; OnPropertyChanged(); } }

        private string _stockOutNote = "";
        public string StockOutNote { get => _stockOutNote; set { _stockOutNote = value; OnPropertyChanged(); } }

        // --- HISTORY ---
        public ObservableCollection<StockBatch> BatchHistory { get; } = new();

        // --- COMMANDS ---
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

        // --- DATA LOADING LOGIC ---
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
                    if (parent != null && (cats.Contains(parent) || string.IsNullOrWhiteSpace(CategorySearchText)))
                        parent.SubCategories.Add(c);
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
            var batches = await _stockRepo.GetAllBatchesAsync();
            var relevant = batches.Where(b => b.ProductId == productId).OrderByDescending(b => b.ReceivedDate);
            foreach (var b in relevant) BatchHistory.Add(b);
        }

        // --- EXECUTION LOGIC ---
        private async Task ExecuteStockIn()
        {
            if (SelectedProduct == null) return;
            if (StockInQty <= 0) { MessageBox.Show("Quantity must be greater than 0"); return; }

            // 1. Generate the Code
            string secretCode = GenerateSmartDiscountCode(StockInDiscount);

            var batch = new StockBatch
            {
                ProductId = SelectedProduct.Id,
                InitialQuantity = StockInQty,
                RemainingQuantity = StockInQty,
                CostPrice = StockInCost,
                SellingPrice = StockInSellingPrice,
                Discount = StockInDiscount,
                DiscountCode = secretCode, // 2. Save Code
                ReceivedDate = StockInDate
            };

            await _stockRepo.AddStockBatchAsync(batch);

            // Update Main Product Totals
            SelectedProduct.Quantity += StockInQty;
            SelectedProduct.BuyingPrice = StockInCost;
            SelectedProduct.SellingPrice = StockInSellingPrice;
            SelectedProduct.DiscountLimit = StockInDiscount;

            await _productRepo.UpdateAsync(SelectedProduct);
            MessageBox.Show($"Stock Added!\nDiscount Code Generated: {secretCode}");

            RefreshView();
        }

        private async Task ExecuteStockOut()
        {
            if (SelectedProduct == null) return;
            if (StockOutQty <= 0) { MessageBox.Show("Quantity must be greater than 0"); return; }
            if (StockOutQty > SelectedProduct.Quantity) { MessageBox.Show("Cannot remove more than current stock!"); return; }

            var movement = new StockMovement
            {
                ProductId = SelectedProduct.Id,
                Quantity = StockOutQty,
                Type = StockMovementType.Adjustment,
                Date = StockOutDate,
                Note = $"{StockOutReason}: {StockOutNote}"
            };

            await _stockRepo.AdjustStockAsync(movement);

            // Note: Ideally, subtract from specific batches here (FIFO), 
            // but for now we just update the total product count via the repo.

            MessageBox.Show("Stock Adjustment Recorded.");
            RefreshView();
        }

        private void RefreshView()
        {
            ClearInputs();
            if (SelectedProduct != null) LoadBatchHistory(SelectedProduct.Id);

            // Refresh list
            var currentSearch = ProductSearchText;
            LoadProductsForCategory();
            ProductSearchText = currentSearch;
        }

        private void ClearInputs()
        {
            StockInQty = 0; StockInCost = 0; StockInSellingPrice = 0; StockInDiscount = 0; StockInDate = DateTime.Now;
            StockOutQty = 0; StockOutNote = ""; StockOutDate = DateTime.Now;
            StockOutReason = "Correction";

            OnPropertyChanged(nameof(StockInQty)); OnPropertyChanged(nameof(StockInCost));
            OnPropertyChanged(nameof(StockInSellingPrice)); OnPropertyChanged(nameof(StockInDiscount));
            OnPropertyChanged(nameof(GeneratedSecretCode));

            OnPropertyChanged(nameof(StockOutQty)); OnPropertyChanged(nameof(StockOutNote));
            OnPropertyChanged(nameof(StockOutReason));
        }

        // --- NEW GENERATOR LOGIC ---
        private string GenerateSmartDiscountCode(double discount)
        {
            // Format: Random(0-9) - Discount(000) - Random(0-9)
            // Example: 10% -> 50109
            var rnd = new Random();
            int firstRandom = rnd.Next(0, 10);
            int lastRandom = rnd.Next(0, 10);

            // Ensure discount is integer for code generation
            int discInt = (int)discount;
            string middlePart = discInt.ToString("000"); // Pads with zeros: 5->"005", 10->"010"

            return $"{firstRandom}{middlePart}{lastRandom}";
        }
    }
}