using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using InventorySystem.UI.Views;

namespace InventorySystem.UI.ViewModels
{
    public class InventoryViewModel : ViewModelBase
    {
        private readonly IProductRepository _productRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IStockRepository _stockRepo;

        // Cache
        private List<Category> _allCategoriesCache = new();
        private List<Product> _allProductsInFolderCache = new();

        public ObservableCollection<Category> CategoryTree { get; } = new();
        public ObservableCollection<Product> Products { get; } = new();

        // --- POPUP: Product Details ---
        private Product? _viewingProduct;
        public Product? ViewingProduct
        {
            get => _viewingProduct;
            set { _viewingProduct = value; OnPropertyChanged(); }
        }

        public ObservableCollection<StockBatch> ProductBatches { get; } = new();

        private bool _isDetailVisible;
        public bool IsDetailVisible
        {
            get => _isDetailVisible;
            set { _isDetailVisible = value; OnPropertyChanged(); }
        }

        // --- SELECTION ---
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

        // --- SEARCH ---
        private string _categorySearchText = "";
        public string CategorySearchText
        {
            get => _categorySearchText;
            set
            {
                _categorySearchText = value;
                OnPropertyChanged();
                FilterCategoryTree();
            }
        }

        private string _productSearchText = "";
        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                _productSearchText = value;
                OnPropertyChanged();
                FilterProductList();
            }
        }

        public string NewCategoryName { get; set; } = "";

        // --- COMMANDS ---
        public ICommand SetSelectedCommand { get; }
        public ICommand AddMainCategoryCommand { get; }
        public ICommand AddSubCategoryCommand { get; }
        public ICommand DeleteCategoryCommand { get; }

        public ICommand AddProductCommand { get; }
        public ICommand EditProductCommand { get; } // <--- ADDED THIS
        public ICommand DeleteProductCommand { get; }

        public ICommand ViewProductCommand { get; }
        public ICommand CloseDetailCommand { get; }

        // Constructor
        public InventoryViewModel(IProductRepository productRepo, ICategoryRepository categoryRepo, IStockRepository stockRepo)
        {
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;
            _stockRepo = stockRepo;

            ViewProductCommand = new RelayCommand<Product>(async (p) => await OpenProductDetail(p));
            CloseDetailCommand = new RelayCommand(() => IsDetailVisible = false);

            SetSelectedCommand = new RelayCommand<Category>(c => SelectedCategory = c);

            AddMainCategoryCommand = new RelayCommand(async () => await AddCategoryAsync(null));
            AddSubCategoryCommand = new RelayCommand(async () =>
            {
                if (SelectedCategory == null) { MessageBox.Show("Select a Main Category first!"); return; }
                await AddCategoryAsync(SelectedCategory.Id);
            });

            DeleteCategoryCommand = new RelayCommand(async () => await DeleteCategoryAsync());

            // 1. ADD PRODUCT LOGIC
            AddProductCommand = new RelayCommand(() =>
            {
                if (SelectedCategory == null) { MessageBox.Show("Please select a Category first!"); return; }

                var vm = new AddProductViewModel(
                    _productRepo,
                    _categoryRepo,
                    productToEdit: null,
                    preSelectedCategoryId: SelectedCategory.Id
                );

                OpenAddEditWindow(vm);
            });

            // 2. EDIT PRODUCT LOGIC (ADDED THIS)
            EditProductCommand = new RelayCommand<Product>((p) =>
            {
                if (p == null) return;
                var vm = new AddProductViewModel(_productRepo, _categoryRepo, productToEdit: p);
                OpenAddEditWindow(vm);
            });

            // 3. DELETE PRODUCT LOGIC
            DeleteProductCommand = new RelayCommand<Product>(async (p) =>
            {
                if (MessageBox.Show("Delete product?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    await _productRepo.DeleteAsync(p);
                    LoadProductsForSelected();
                }
            });

            LoadTree();
        }

        // --- HELPER: Open the Add/Edit Window ---
        private void OpenAddEditWindow(AddProductViewModel vm)
        {
            var window = new AddProductWindow(); // Now it will find this class
            window.DataContext = vm;

            vm.CloseAction = () =>
            {
                window.Close();
                LoadProductsForSelected();
            };

            window.ShowDialog();
        }

        private async Task OpenProductDetail(Product p)
        {
            if (p == null) return;
            ViewingProduct = p;

            var allBatches = await _stockRepo.GetAllBatchesAsync();
            var specificBatches = allBatches.Where(b => b.ProductId == p.Id).OrderByDescending(b => b.ReceivedDate).ToList();

            ProductBatches.Clear();
            foreach (var b in specificBatches) ProductBatches.Add(b);

            IsDetailVisible = true;
        }

        private async void LoadTree()
        {
            var all = await _categoryRepo.GetAllAsync();
            _allCategoriesCache = all.ToList();

            // Rebuild Hierarchy
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
                foreach (var c in _allCategoriesCache.Where(c => c.ParentId == null))
                    CategoryTree.Add(c);
            }
            else
            {
                var lowerText = CategorySearchText.ToLower();
                foreach (var c in _allCategoriesCache.Where(c => c.Name.ToLower().Contains(lowerText)))
                    CategoryTree.Add(c);
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
            {
                var lowerText = ProductSearchText.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(lowerText));
            }

            foreach (var p in query) Products.Add(p);
        }

        private async Task AddCategoryAsync(int? parentId)
        {
            if (string.IsNullOrWhiteSpace(NewCategoryName)) return;

            var cat = new Category { Name = NewCategoryName, ParentId = parentId };
            await _categoryRepo.AddAsync(cat);

            NewCategoryName = "";
            OnPropertyChanged(nameof(NewCategoryName));
            LoadTree();
        }

        private async Task DeleteCategoryAsync()
        {
            if (SelectedCategory == null) return;
            if (MessageBox.Show($"Delete '{SelectedCategory.Name}' and all items inside?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await _categoryRepo.DeleteAsync(SelectedCategory);
                SelectedCategory = null;
                LoadTree();
            }
        }
    }
}