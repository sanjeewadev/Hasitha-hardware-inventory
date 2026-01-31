using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using InventorySystem.UI.Views; // For EditBatchWindow & AddProductWindow
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

        // --- MAIN LIST DATA ---
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<Category> Categories { get; } = new();

        private List<Product> _allProductsCache = new();

        // --- FILTERS ---
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterList(); }
        }

        private Category? _selectedCategory;
        public Category? SelectedCategory
        {
            get => _selectedCategory;
            set { _selectedCategory = value; OnPropertyChanged(); FilterList(); }
        }

        // --- POPUP DATA (View Details) ---
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
            set { _viewingProduct = value; OnPropertyChanged(); }
        }

        public ObservableCollection<StockBatch> ProductBatches { get; } = new();

        // --- COMMANDS ---
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearFilterCommand { get; }

        // Popup Commands
        public ICommand ViewCommand { get; }
        public ICommand CloseDetailCommand { get; }
        public ICommand EditBatchCommand { get; }
        public ICommand DeleteBatchCommand { get; }

        // --- CONSTRUCTOR ---
        public ProductViewModel(IProductRepository pRepo, ICategoryRepository cRepo, IStockRepository sRepo)
        {
            _productRepo = pRepo;
            _categoryRepo = cRepo;
            _stockRepo = sRepo;

            // Main List Actions
            AddCommand = new RelayCommand(OpenAddProductWindow);
            EditCommand = new RelayCommand<Product>(OpenEditProductWindow);
            DeleteCommand = new RelayCommand<Product>(async (p) => await DeleteProductAsync(p));
            ClearFilterCommand = new RelayCommand(() => { SearchText = ""; SelectedCategory = null; });

            // Popup Actions
            ViewCommand = new RelayCommand<Product>(async (p) => await OpenProductDetail(p));
            CloseDetailCommand = new RelayCommand(() => IsDetailVisible = false);

            // Batch Actions
            EditBatchCommand = new RelayCommand<StockBatch>(OpenEditBatchWindow);
            DeleteBatchCommand = new RelayCommand<StockBatch>(async (b) => await DeleteBatchAsync(b));

            LoadData();
        }

        // --- DATA LOADING ---
        private async void LoadData()
        {
            // 1. Load Categories (Roots only)
            var cats = await _categoryRepo.GetAllAsync();
            Categories.Clear();
            foreach (var c in cats.Where(c => c.ParentId == null))
            {
                Categories.Add(c);
            }

            // 2. Load Products
            var prods = await _productRepo.GetAllAsync();
            _allProductsCache = prods.ToList();

            FilterList();
        }

        private void FilterList()
        {
            Products.Clear();
            var query = _allProductsCache.AsEnumerable();

            if (SelectedCategory != null)
                query = query.Where(p => p.CategoryId == SelectedCategory.Id);

            if (!string.IsNullOrWhiteSpace(SearchText))
                query = query.Where(p => p.Name.ToLower().Contains(SearchText.ToLower()));

            foreach (var p in query) Products.Add(p);
        }

        // --- POPUP LOGIC ---
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
            var allBatches = await _stockRepo.GetAllBatchesAsync();
            var specificBatches = allBatches
                .Where(b => b.ProductId == ViewingProduct.Id)
                .OrderByDescending(b => b.ReceivedDate)
                .ToList();

            ProductBatches.Clear();
            foreach (var b in specificBatches) ProductBatches.Add(b);
        }

        // --- WINDOW ACTIONS ---
        private void OpenAddProductWindow()
        {
            // Note: AddProductViewModel handles the "Auto Generate Barcode" logic
            var vm = new AddProductViewModel(_productRepo, _categoryRepo);
            var win = new AddProductWindow { DataContext = vm };
            vm.CloseAction = () => { win.Close(); LoadData(); };
            win.ShowDialog();
        }

        private void OpenEditProductWindow(Product p)
        {
            if (p == null) return;
            var vm = new AddProductViewModel(_productRepo, _categoryRepo, p);
            var win = new AddProductWindow { DataContext = vm };
            vm.CloseAction = () => { win.Close(); LoadData(); };
            win.ShowDialog();
        }

        private async Task DeleteProductAsync(Product p)
        {
            if (MessageBox.Show($"Delete '{p.Name}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await _productRepo.DeleteAsync(p);
                LoadData();
            }
        }

        // --- BATCH EDITING ---
        private void OpenEditBatchWindow(StockBatch batch)
        {
            if (batch == null) return;
            var vm = new EditBatchViewModel(_stockRepo, batch);
            var win = new EditBatchWindow { DataContext = vm };
            vm.CloseAction = () => { win.Close(); _ = LoadBatchesForViewingProduct(); };
            win.ShowDialog();
        }

        private async Task DeleteBatchAsync(StockBatch batch)
        {
            if (batch == null) return;
            if (MessageBox.Show("Delete this batch permanently?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                await _stockRepo.DeleteBatchAsync(batch);

                if (ViewingProduct != null)
                {
                    ViewingProduct.Quantity -= batch.RemainingQuantity;
                    await _productRepo.UpdateAsync(ViewingProduct);
                }

                await LoadBatchesForViewingProduct();
            }
        }
    }
}