using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class AdjustmentViewModel : ViewModelBase
    {
        private readonly IProductRepository _productRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IStockRepository _stockRepo;
        private readonly Data.Context.InventoryDbContext _dbContext;

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
                FilterProducts();
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
                    LoadDataForSelectedProduct();
                }
                else
                {
                    ClearInputs();
                }
            }
        }

        public bool IsProductSelected => SelectedProduct != null;
        public string CurrentUnit => SelectedProduct?.Unit ?? "";

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
        public ObservableCollection<StockMovement> AdjustmentHistory { get; } = new();

        public ICommand StockOutCommand { get; }

        public AdjustmentViewModel(IProductRepository pRepo, ICategoryRepository cRepo, IStockRepository sRepo)
        {
            // CRITICAL FIX 1: Override injected repos with fresh ones to destroy the Stale Cache bug!
            _dbContext = Infrastructure.Services.DatabaseService.CreateDbContext();
            _productRepo = new ProductRepository(_dbContext);
            _categoryRepo = new CategoryRepository(_dbContext);
            _stockRepo = new StockRepository(_dbContext);

            StockOutCommand = new RelayCommand(async () => await ExecuteStockOut());

            LoadInitialData();
        }

        private async void LoadInitialData()
        {
            var cats = await _categoryRepo.GetAllAsync();
            _allCategoriesCache = cats.ToList();
            FilterCategoryTree();

            var prods = await _productRepo.GetAllAsync();
            _allProductsCache = prods.ToList();
            FilterProducts();
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

        private void FilterProducts()
        {
            ProductsInSelectedCategory.Clear();
            var query = _allProductsCache.AsEnumerable();

            if (SelectedCategory != null) query = query.Where(p => p.CategoryId == SelectedCategory.Id);

            if (!string.IsNullOrWhiteSpace(ProductSearchText))
            {
                var lower = ProductSearchText.ToLower();
                query = _allProductsCache.Where(p => p.Name.ToLower().Contains(lower) || p.Barcode.ToLower().Contains(lower));
            }

            foreach (var p in query) ProductsInSelectedCategory.Add(p);
        }

        private async void LoadDataForSelectedProduct()
        {
            if (SelectedProduct == null) return;

            ActiveBatches.Clear();

            // CRITICAL FIX 2: Only load batches that belong to POSTED invoices (No Draft Leaks!)
            var batches = await _stockRepo.GetActiveBatchesAsync();
            var relevantBatches = batches
                .Where(b => b.ProductId == SelectedProduct.Id)
                .OrderByDescending(b => b.ReceivedDate);

            foreach (var b in relevantBatches) ActiveBatches.Add(b);
            SelectedAdjustmentBatch = ActiveBatches.FirstOrDefault();

            AdjustmentHistory.Clear();
            var history = await _dbContext.StockMovements
                .Where(m => m.ProductId == SelectedProduct.Id && m.Type == StockMovementType.Adjustment)
                .OrderByDescending(m => m.Date)
                .ToListAsync();

            foreach (var h in history) AdjustmentHistory.Add(h);
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

            decimal financialLoss = StockOutReason == AdjustmentReason.Correction ? 0 : (StockOutQty * SelectedAdjustmentBatch.CostPrice);
            string lossWarning = financialLoss > 0 ? $"\n\n⚠️ FINANCIAL LOSS: Rs {financialLoss:N2}" : "\n\n(No financial loss recorded for Correction)";
            var msg = $"Confirm Adjustment?\n\nRemoving: {StockOutQty} {SelectedProduct.Unit}\nReason: {StockOutReason}{lossWarning}\n\nThis cannot be undone.";

            if (MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                var movement = new StockMovement
                {
                    ProductId = SelectedProduct.Id,
                    Quantity = StockOutQty,
                    Type = StockMovementType.Adjustment,
                    Date = DateTime.Now,
                    Reason = StockOutReason,
                    StockBatchId = SelectedAdjustmentBatch.Id,
                    Note = $"Manual Adjustment: {StockOutReason}",
                    UnitCost = SelectedAdjustmentBatch.CostPrice,
                    UnitPrice = SelectedAdjustmentBatch.SellingPrice
                };

                await _stockRepo.AdjustStockAsync(movement);

                // CRITICAL FIX 3: Pull the exact live quantity from DB so the UI updates correctly
                var freshProduct = await _dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == SelectedProduct.Id);
                if (freshProduct != null)
                {
                    SelectedProduct.Quantity = freshProduct.Quantity;
                    OnPropertyChanged(nameof(SelectedProduct));
                }

                MessageBox.Show("Stock Adjusted Successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                LoadDataForSelectedProduct();
                StockOutQty = 0;
            }
        }

        private void ClearInputs()
        {
            StockOutQty = 0;
            StockOutDate = DateTime.Now;
            StockOutReason = AdjustmentReason.Correction;
            SelectedAdjustmentBatch = null;
            AdjustmentHistory.Clear();
            ActiveBatches.Clear();

            OnPropertyChanged(nameof(StockOutQty));
            OnPropertyChanged(nameof(StockOutReason));
            OnPropertyChanged(nameof(SelectedAdjustmentBatch));
        }
    }
}