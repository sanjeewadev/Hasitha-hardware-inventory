using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using InventorySystem.UI.Views;
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

        // --- COLLECTIONS ---
        private List<Product> _allProductsCache = new();
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<StockBatch> ProductBatches { get; } = new();
        public ObservableCollection<StockMovement> ProductHistory { get; } = new();

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
        public ICommand EditBatchCommand { get; }
        public ICommand DeleteBatchCommand { get; }

        public ProductViewModel(IProductRepository productRepo, ICategoryRepository categoryRepo, IStockRepository stockRepo)
        {
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;
            _stockRepo = stockRepo;

            LoadCommand = new RelayCommand(async () => await LoadData());
            ClearFilterCommand = new RelayCommand(() => SearchText = "");

            ViewCommand = new RelayCommand<Product>(async (p) => await OpenProductDetail(p));
            CloseDetailCommand = new RelayCommand(() => IsDetailVisible = false);

            DeleteProductCommand = new RelayCommand<Product>(async (p) => await AttemptDeleteProduct(p));

            EditBatchCommand = new RelayCommand<StockBatch>(OpenEditBatchWindow);
            DeleteBatchCommand = new RelayCommand<StockBatch>(async (b) => await DeleteBatchAsync(b));

            LoadData();
        }

        private async Task AttemptDeleteProduct(Product p)
        {
            if (p == null) return;

            // CHECK 1: STOCK
            if (p.Quantity > 0)
            {
                MessageBox.Show($"You cannot delete '{p.Name}' because it still has stock ({p.Quantity}).\n\nPlease remove all stock using the Adjustment tab first.", "Deletion Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // CHECK 2: HISTORY
            var allHistory = await _stockRepo.GetHistoryAsync();
            if (allHistory.Any(x => x.ProductId == p.Id))
            {
                MessageBox.Show($"The product '{p.Name}' has linked sales records.\n\nDeleting it will corrupt your past Sales Reports.\n\nWe recommend keeping it for records.", "Restricted Action", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // CHECK 3: CONFIRM
            if (MessageBox.Show($"Are you sure you want to permanently delete '{p.Name}'?\nThis action cannot be undone.", "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _productRepo.DeleteAsync(p.Id);
                await LoadData();
                MessageBox.Show($"Product '{p.Name}' has been removed.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
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

        // --- UPDATED: 7-Day Retention Logic ---
        private async Task LoadBatchesForViewingProduct()
        {
            if (ViewingProduct == null) return;

            var allBatches = await _stockRepo.GetAllBatchesAsync();

            // LOGIC: Keep if Quantity > 0 OR if created within last 7 days
            var cutoffDate = DateTime.Now.AddDays(-7);

            var specificBatches = allBatches
                .Where(b => b.ProductId == ViewingProduct.Id)
                .Where(b => b.RemainingQuantity > 0 || b.ReceivedDate >= cutoffDate)
                .OrderByDescending(b => b.ReceivedDate)
                .ToList();

            ProductBatches.Clear();
            foreach (var b in specificBatches) ProductBatches.Add(b);

            var allHistory = await _stockRepo.GetHistoryAsync();
            var specificHistory = allHistory.Where(m => m.ProductId == ViewingProduct.Id).OrderByDescending(m => m.Date).ToList();

            ProductHistory.Clear();
            foreach (var h in specificHistory) ProductHistory.Add(h);
        }

        private void OpenEditBatchWindow(StockBatch batch)
        {
            if (batch == null) return;
            var vm = new EditBatchViewModel(_stockRepo, batch);
            var win = new EditBatchWindow { DataContext = vm };
            vm.CloseAction = async () =>
            {
                win.Close();
                await LoadBatchesForViewingProduct();
                await LoadData();
            };
            win.ShowDialog();
        }

        private async Task DeleteBatchAsync(StockBatch batch)
        {
            if (MessageBox.Show("Delete this batch record? Stock will be reduced.", "Confirm Batch Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
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