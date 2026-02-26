using InventorySystem.Core.Entities;
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
    public class ProductViewModel : ViewModelBase
    {
        private readonly IProductRepository _productRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IStockRepository _stockRepo;
        private readonly Data.Context.InventoryDbContext _dbContext;

        // --- COLLECTIONS ---
        private List<Product> _allProductsCache = new();
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<StockBatch> ProductBatches { get; } = new();

        // --- SEARCH ---
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterProducts(); }
        }

        // --- POPUP STATE ---
        private bool _isDetailVisible;
        public bool IsDetailVisible
        {
            get => _isDetailVisible;
            set { _isDetailVisible = value; OnPropertyChanged(); }
        }

        private Product? _viewingProduct;
        public Product? ViewingProduct
        {
            get => _viewingProduct;
            set
            {
                _viewingProduct = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentUnit));
            }
        }

        public string CurrentUnit => ViewingProduct?.Unit ?? "";

        // --- COMMANDS ---
        public ICommand LoadCommand { get; }
        public ICommand ClearFilterCommand { get; }
        public ICommand DeleteProductCommand { get; }
        public ICommand ViewCommand { get; }
        public ICommand CloseDetailCommand { get; }
        public ICommand DeleteBatchCommand { get; }

        public ProductViewModel(IProductRepository productRepo, ICategoryRepository categoryRepo, IStockRepository stockRepo)
        {
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;
            _stockRepo = stockRepo;
            _dbContext = Infrastructure.Services.DatabaseService.CreateDbContext();

            LoadCommand = new RelayCommand(async () => await LoadData());
            ClearFilterCommand = new RelayCommand(() => SearchText = "");

            ViewCommand = new RelayCommand<Product>(async (p) => await OpenProductDetail(p));
            CloseDetailCommand = new RelayCommand(() => IsDetailVisible = false);

            DeleteProductCommand = new RelayCommand<Product>(async (p) => await AttemptDeleteProduct(p));
            DeleteBatchCommand = new RelayCommand<StockBatch>(async (b) => await DeleteBatchAsync(b));

            LoadData();
        }

        private async Task AttemptDeleteProduct(Product p)
        {
            if (p == null) return;

            if (p.Quantity > 0)
            {
                MessageBox.Show($"You cannot delete '{p.Name}' because it still has stock ({p.Quantity}).\n\nPlease remove the stock first.", "Deletion Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var allHistory = await _stockRepo.GetHistoryAsync();
            if (allHistory.Any(x => x.ProductId == p.Id))
            {
                MessageBox.Show($"The product '{p.Name}' has linked financial sales records. To protect your accounting history, this product cannot be deleted.", "System Protected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Permanently delete the catalog entry for '{p.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                await _productRepo.DeleteAsync(p.Id);
                await LoadData();
                MessageBox.Show("Product successfully deleted.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task LoadData()
        {
            // 1. Fetch all categories first
            var allCategories = await _categoryRepo.GetAllAsync();
            var categoryDict = allCategories.ToDictionary(c => c.Id);

            // 2. Fetch all products
            var list = await _productRepo.GetAllAsync();

            // 3. Map the categories in-memory (No GetByIdAsync needed! Much faster.)
            foreach (var p in list)
            {
                if (p.CategoryId > 0 && p.Category == null && categoryDict.ContainsKey(p.CategoryId))
                {
                    p.Category = categoryDict[p.CategoryId];
                }
            }

            _allProductsCache = list.ToList();
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

        private async Task OpenProductDetail(Product p)
        {
            if (p == null) return;
            ViewingProduct = p;
            await LoadBatchesForViewingProduct();
            IsDetailVisible = true;
        }

        private async Task LoadBatchesForViewingProduct()
        {
            if (ViewingProduct == null) return;

            var allBatches = await _dbContext.StockBatches
                .Include(b => b.PurchaseInvoice)
                .ThenInclude(i => i.Supplier)
                .Where(b => b.ProductId == ViewingProduct.Id)
                .OrderByDescending(b => b.ReceivedDate)
                .ToListAsync();

            // Filter: Keep if Quantity > 0 OR if created within last 7 days
            var cutoffDate = DateTime.Now.AddDays(-7);
            var visibleBatches = allBatches
                .Where(b => b.RemainingQuantity > 0 || b.ReceivedDate >= cutoffDate)
                .ToList();

            ProductBatches.Clear();
            foreach (var b in visibleBatches) ProductBatches.Add(b);
        }

        private async Task DeleteBatchAsync(StockBatch batch)
        {
            // FIX: VULNERABILITY PREVENTION
            if (batch.InitialQuantity != batch.RemainingQuantity)
            {
                MessageBox.Show("This batch cannot be deleted because items from it have already been sold.\n\nDeleting it would corrupt your financial and sales history.\nIf the stock is damaged, please use the 'Remove Stock' page instead.", "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to completely erase this batch of {batch.RemainingQuantity} items?\nThis will permanently remove it from your inventory.", "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                await _stockRepo.DeleteBatchAsync(batch);

                if (ViewingProduct != null)
                {
                    ViewingProduct.Quantity -= batch.RemainingQuantity;
                    if (ViewingProduct.Quantity < 0) ViewingProduct.Quantity = 0;

                    _dbContext.Products.Update(ViewingProduct);
                    await _dbContext.SaveChangesAsync();
                }

                await LoadBatchesForViewingProduct();
                await LoadData();
            }
        }
    }
}