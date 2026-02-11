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

        // --- SELECTION STATE ---
        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                _selectedProduct = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsProductSelected));
                OnPropertyChanged(nameof(CurrentUnit));

                if (_selectedProduct != null)
                {
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
        public string CurrentUnit => SelectedProduct?.Unit ?? "";

        // --- ADJUSTMENT (OUT) PROPERTIES ---
        public DateTime StockOutDate { get; set; } = DateTime.Now;

        private decimal _stockOutQty;
        public decimal StockOutQty { get => _stockOutQty; set { _stockOutQty = value; OnPropertyChanged(); } }

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

        public ICommand StockOutCommand { get; }

        public StockViewModel(IProductRepository pRepo, ICategoryRepository cRepo, IStockRepository sRepo)
        {
            _productRepo = pRepo;
            _categoryRepo = cRepo;
            _stockRepo = sRepo;

            StockOutCommand = new RelayCommand(async () => await ExecuteStockOut());

            LoadTree();
        }

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

        private async Task ExecuteStockOut()
        {
            if (SelectedProduct == null) return;
            if (SelectedAdjustmentBatch == null) { MessageBox.Show("Please select a specific batch to adjust.", "Batch Required", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            if (StockOutQty <= 0)
            {
                MessageBox.Show("Quantity must be greater than 0.", "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StockOutQty > SelectedAdjustmentBatch.RemainingQuantity)
            {
                MessageBox.Show($"Cannot remove {StockOutQty}. The selected batch only has {SelectedAdjustmentBatch.RemainingQuantity} remaining.", "Insufficient Stock", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var msg = $"Are you sure you want to remove {StockOutQty} {SelectedProduct.Unit}?\nReason: {StockOutReason}";
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
                MessageBox.Show("Stock Adjusted Successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
            StockOutQty = 0;
            StockOutDate = DateTime.Now;
            StockOutReason = AdjustmentReason.Correction;
            SelectedAdjustmentBatch = null;

            OnPropertyChanged(nameof(StockOutQty));
            OnPropertyChanged(nameof(StockOutReason));
            OnPropertyChanged(nameof(SelectedAdjustmentBatch));
        }
    }
}