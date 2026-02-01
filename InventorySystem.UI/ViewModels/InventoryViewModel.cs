using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using InventorySystem.UI.Views;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class InventoryViewModel : ViewModelBase
    {
        private readonly IProductRepository _productRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IStockRepository _stockRepo;

        private List<Category> _allCategoriesCache = new();
        private List<Product> _allProductsInFolderCache = new();

        public ObservableCollection<Category> CategoryTree { get; } = new();
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<StockBatch> ProductBatches { get; } = new();

        // --- PROPERTIES ---
        private Product? _viewingProduct;
        public Product? ViewingProduct
        {
            get => _viewingProduct;
            set { _viewingProduct = value; OnPropertyChanged(); }
        }

        private bool _isDetailVisible;
        public bool IsDetailVisible
        {
            get => _isDetailVisible;
            set { _isDetailVisible = value; OnPropertyChanged(); }
        }

        private Category? _selectedCategory;
        public Category? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                OnPropertyChanged();
                LoadProductsForSelected();
            }
        }

        public string NewCategoryName { get; set; } = "";

        // Search properties
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
            set { _productSearchText = value; OnPropertyChanged(); FilterProductList(); }
        }

        // --- COMMANDS ---
        public ICommand AddMainCategoryCommand { get; }
        public ICommand AddSubCategoryCommand { get; }
        public ICommand DeleteCategoryCommand { get; }
        public ICommand AddProductCommand { get; }
        public ICommand EditProductCommand { get; }
        public ICommand DeleteProductCommand { get; }
        public ICommand ViewProductCommand { get; }
        public ICommand CloseDetailCommand { get; }
        public ICommand EditBatchCommand { get; }
        public ICommand DeleteBatchCommand { get; }

        public InventoryViewModel(IProductRepository productRepo, ICategoryRepository categoryRepo, IStockRepository stockRepo)
        {
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;
            _stockRepo = stockRepo;

            ViewProductCommand = new RelayCommand<Product>(async (p) => await OpenProductDetail(p));
            CloseDetailCommand = new RelayCommand(() => IsDetailVisible = false);

            AddMainCategoryCommand = new RelayCommand(async () => await AddCategoryAsync(null));
            AddSubCategoryCommand = new RelayCommand(async () =>
            {
                if (SelectedCategory == null) { MessageBox.Show("Please select a Main Category first!", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                await AddCategoryAsync(SelectedCategory.Id);
            });

            DeleteCategoryCommand = new RelayCommand(async () => await DeleteCategoryAsync());

            AddProductCommand = new RelayCommand(() =>
            {
                if (SelectedCategory == null) { MessageBox.Show("Please select a Category folder first!", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var vm = new AddProductViewModel(_productRepo, _categoryRepo, null, SelectedCategory.Id);
                OpenAddEditWindow(vm);
            });

            EditProductCommand = new RelayCommand<Product>((p) =>
            {
                if (p == null) return;
                var vm = new AddProductViewModel(_productRepo, _categoryRepo, p);
                OpenAddEditWindow(vm);
            });

            // --- SMART DELETE COMMAND ---
            DeleteProductCommand = new RelayCommand<Product>(async (p) => await AttemptDeleteProduct(p));

            EditBatchCommand = new RelayCommand<StockBatch>(OpenEditBatchWindow);
            DeleteBatchCommand = new RelayCommand<StockBatch>(async (b) => await DeleteBatchAsync(b));

            LoadTree();
        }

        // --- 1. SMART PRODUCT DELETION LOGIC ---
        private async Task AttemptDeleteProduct(Product p)
        {
            if (p == null) return;

            // CHECK 1: STOCK (Blocking Error)
            if (p.Quantity > 0)
            {
                MessageBox.Show(
                    $"Deletion Blocked:\n\nYou cannot delete '{p.Name}' because it still has stock ({p.Quantity}).\n\nPlease remove all stock using the Adjustment tab first.",
                    "Active Stock Detected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Stop);
                return;
            }

            // CHECK 2: HISTORY (Integrity Warning)
            var allHistory = await _stockRepo.GetHistoryAsync();
            bool hasHistory = allHistory.Any(x => x.ProductId == p.Id);

            if (hasHistory)
            {
                MessageBox.Show(
                    $"Restricted Action:\n\nThe product '{p.Name}' has sales or movement history.\nDeleting it will create 'Unknown Product' entries in your past reports.\n\nIt is better to just keep it with 0 stock.",
                    "Data Integrity Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // CHECK 3: FINAL CONFIRMATION
            if (MessageBox.Show($"Are you sure you want to delete '{p.Name}'?\nThis cannot be undone.", "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _productRepo.DeleteAsync(p.Id);
                LoadProductsForSelected();
                MessageBox.Show("Product deleted successfully.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // --- 2. SMART CATEGORY DELETION LOGIC ---
        private async Task DeleteCategoryAsync()
        {
            if (SelectedCategory == null) return;

            // CHECK 1: Sub-Categories
            if (SelectedCategory.SubCategories.Count > 0)
            {
                MessageBox.Show(
                    $"Cannot delete '{SelectedCategory.Name}'.\n\nIt contains {SelectedCategory.SubCategories.Count} sub-folders. Please delete them first.",
                    "Folder Not Empty",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            // CHECK 2: Products Inside
            var allProducts = await _productRepo.GetAllAsync();
            int productCount = allProducts.Count(p => p.CategoryId == SelectedCategory.Id);

            if (productCount > 0)
            {
                MessageBox.Show(
                    $"Cannot delete '{SelectedCategory.Name}'.\n\nIt contains {productCount} products. Please move or delete the products first.",
                    "Folder Not Empty",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            // CHECK 3: Confirm
            if (MessageBox.Show($"Delete category '{SelectedCategory.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _categoryRepo.DeleteAsync(SelectedCategory);
                SelectedCategory = null;
                LoadTree();
            }
        }

        // --- 3. SMART ADD CATEGORY LOGIC ---
        private async Task AddCategoryAsync(int? parentId)
        {
            if (string.IsNullOrWhiteSpace(NewCategoryName))
            {
                MessageBox.Show("Please enter a category name.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check Duplicates
            var exists = _allCategoriesCache.Any(c => c.Name.ToLower() == NewCategoryName.ToLower() && c.ParentId == parentId);
            if (exists)
            {
                MessageBox.Show($"A category named '{NewCategoryName}' already exists here.", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            var cat = new Category { Name = NewCategoryName, ParentId = parentId };
            await _categoryRepo.AddAsync(cat);
            NewCategoryName = "";
            OnPropertyChanged(nameof(NewCategoryName));
            LoadTree();
        }

        // --- HELPER METHODS ---
        private void OpenAddEditWindow(AddProductViewModel vm)
        {
            var window = new AddProductWindow();
            window.DataContext = vm;
            vm.CloseAction = () => { window.Close(); LoadProductsForSelected(); };
            window.ShowDialog();
        }

        private async Task OpenProductDetail(Product p)
        {
            if (p == null) return;
            ViewingProduct = p;
            LoadBatchesForViewingProduct();
            IsDetailVisible = true;
        }

        private async void LoadBatchesForViewingProduct()
        {
            if (ViewingProduct == null) return;
            var allBatches = await _stockRepo.GetAllBatchesAsync();
            var specificBatches = allBatches.Where(b => b.ProductId == ViewingProduct.Id)
                                            .OrderByDescending(b => b.ReceivedDate).ToList();

            ProductBatches.Clear();
            foreach (var b in specificBatches) ProductBatches.Add(b);
        }

        private async void LoadTree()
        {
            var all = await _categoryRepo.GetAllAsync();
            _allCategoriesCache = all.ToList();
            foreach (var cat in _allCategoriesCache) cat.SubCategories.Clear();
            foreach (var cat in _allCategoriesCache)
            {
                if (cat.ParentId != null)
                {
                    var parent = _allCategoriesCache.FirstOrDefault(c => c.Id == cat.ParentId);
                    if (parent != null) parent.SubCategories.Add(cat);
                }
            }
            FilterCategoryTree();
        }

        private void FilterCategoryTree()
        {
            CategoryTree.Clear();
            if (string.IsNullOrWhiteSpace(CategorySearchText))
            {
                foreach (var c in _allCategoriesCache.Where(c => c.ParentId == null)) CategoryTree.Add(c);
            }
            else
            {
                var lowerText = CategorySearchText.ToLower();
                foreach (var c in _allCategoriesCache.Where(c => c.Name.ToLower().Contains(lowerText))) CategoryTree.Add(c);
            }
        }

        private async void LoadProductsForSelected()
        {
            Products.Clear();
            if (SelectedCategory == null) return;

            var all = await _productRepo.GetAllAsync();
            _allProductsInFolderCache = all.Where(p => p.CategoryId == SelectedCategory.Id).ToList();
            FilterProductList();
        }

        private void FilterProductList()
        {
            Products.Clear();
            var query = _allProductsInFolderCache.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(ProductSearchText))
                query = query.Where(p => p.Name.ToLower().Contains(ProductSearchText.ToLower()));
            foreach (var p in query) Products.Add(p);
        }

        private void OpenEditBatchWindow(StockBatch batch)
        {
            if (batch == null) return;
            var vm = new EditBatchViewModel(_stockRepo, batch);
            var win = new EditBatchWindow { DataContext = vm };
            vm.CloseAction = () => { win.Close(); LoadBatchesForViewingProduct(); };
            win.ShowDialog();
        }

        private async Task DeleteBatchAsync(StockBatch batch)
        {
            if (batch == null) return;

            // Batch Delete Confirmation
            if (MessageBox.Show("Are you sure you want to delete this specific batch record?\nStock quantity will be deducted.", "Confirm Batch Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                await _stockRepo.DeleteBatchAsync(batch);
                if (ViewingProduct != null)
                {
                    ViewingProduct.Quantity -= batch.RemainingQuantity;
                    if (ViewingProduct.Quantity < 0) ViewingProduct.Quantity = 0;
                    await _productRepo.UpdateAsync(ViewingProduct);
                }
                LoadBatchesForViewingProduct();
            }
        }
    }
}