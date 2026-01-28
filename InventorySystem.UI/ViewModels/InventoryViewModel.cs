using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System.Collections.Generic; // Needed for List<T>
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

        // Cache all data so search is fast (don't hit DB every letter)
        private List<Category> _allCategoriesCache = new(); // <--- NEW
        private List<Product> _allProductsInFolderCache = new(); // <--- NEW

        public ObservableCollection<Category> CategoryTree { get; } = new();
        public ObservableCollection<Product> Products { get; } = new();

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

        // --- SEARCH INPUTS ---
        private string _categorySearchText = "";
        public string CategorySearchText // <--- NEW
        {
            get => _categorySearchText;
            set
            {
                _categorySearchText = value;
                OnPropertyChanged();
                FilterCategoryTree(); // Trigger filter when typing
            }
        }

        private string _productSearchText = "";
        public string ProductSearchText // <--- NEW
        {
            get => _productSearchText;
            set
            {
                _productSearchText = value;
                OnPropertyChanged();
                FilterProductList(); // Trigger filter when typing
            }
        }

        public string NewCategoryName { get; set; } = "";

        // --- COMMANDS ---
        public ICommand SetSelectedCommand { get; }
        public ICommand AddMainCategoryCommand { get; }
        public ICommand AddSubCategoryCommand { get; }
        public ICommand DeleteCategoryCommand { get; }
        public ICommand AddProductCommand { get; }
        public ICommand DeleteProductCommand { get; }

        public InventoryViewModel(IProductRepository productRepo, ICategoryRepository categoryRepo)
        {
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;

            SetSelectedCommand = new RelayCommand<Category>(c => SelectedCategory = c);

            AddMainCategoryCommand = new RelayCommand(async () => await AddCategoryAsync(null));
            AddSubCategoryCommand = new RelayCommand(async () =>
            {
                if (SelectedCategory == null) { MessageBox.Show("Select a Main Category first!"); return; }
                await AddCategoryAsync(SelectedCategory.Id);
            });

            DeleteCategoryCommand = new RelayCommand(async () => await DeleteCategoryAsync());

            AddProductCommand = new RelayCommand(() =>
            {
                if (SelectedCategory == null) { MessageBox.Show("Select a Category first!"); return; }
                MessageBox.Show($"Open Add Window for Category: {SelectedCategory.Name}");
            });

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

        private async void LoadTree()
        {
            var all = await _categoryRepo.GetAllAsync();
            _allCategoriesCache = all.ToList(); // Save to cache
            FilterCategoryTree(); // Load the tree using the filter logic (empty filter = full tree)
        }

        private void FilterCategoryTree()
        {
            CategoryTree.Clear();

            // If Search is Empty: Show standard Tree (Parents only)
            if (string.IsNullOrWhiteSpace(CategorySearchText))
            {
                foreach (var c in _allCategoriesCache.Where(c => c.ParentId == null))
                    CategoryTree.Add(c);
            }
            // If Searching: Flatten the list and show ALL matches (ignoring hierarchy for better visibility)
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

            // Get raw list for this folder
            _allProductsInFolderCache = all.Where(p => p.CategoryId == SelectedCategory.Id).ToList();

            // Apply current search text (if any)
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

        // ... AddCategoryAsync and DeleteCategoryAsync remain the same ...
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
            // Corrected Line below:
            if (MessageBox.Show($"Delete '{SelectedCategory.Name}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await _categoryRepo.DeleteAsync(SelectedCategory);
                SelectedCategory = null;
                LoadTree();
            }
        }
    }
}