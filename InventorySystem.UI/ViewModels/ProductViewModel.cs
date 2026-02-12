using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using InventorySystem.UI.Views;
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
        // REMOVED: ProductHistory

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
                MessageBox.Show($"You cannot delete '{p.Name}' because it still has stock ({p.Quantity}).", "Deletion Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var allHistory = await _stockRepo.GetHistoryAsync();
            if (allHistory.Any(x => x.ProductId == p.Id))
            {
                MessageBox.Show($"The product '{p.Name}' has linked sales records. Deletion restricted.", "Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Permanently delete '{p.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _productRepo.DeleteAsync(p.Id);
                await LoadData();
                MessageBox.Show("Product deleted.");
            }
        }

        private async Task LoadData()
        {
            var list = await _productRepo.GetAllAsync();
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

            // LOAD BATCHES (With Supplier Info)
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

            // REMOVED HISTORY LOAD
        }

        private async Task DeleteBatchAsync(StockBatch batch)
        {
            if (MessageBox.Show("Delete this batch record? Stock will be reduced.", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _stockRepo.DeleteBatchAsync(batch);
                if (ViewingProduct != null)
                {
                    ViewingProduct.Quantity -= batch.RemainingQuantity;
                    if (ViewingProduct.Quantity < 0) ViewingProduct.Quantity = 0;
                    await _productRepo.UpdateAsync(ViewingProduct);
                }
                await LoadBatchesForViewingProduct();
                await LoadData();
            }
        }
    }
}