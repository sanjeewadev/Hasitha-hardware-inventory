using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class ProductViewModel : ViewModelBase
    {
        private readonly IProductRepository _productRepo;
        private readonly ICategoryRepository _categoryRepo;
        private readonly IStockRepository _stockRepo; // Needed to see "Branches" (Batches)

        // --- DATA ---
        private List<Product> _allProducts = new();
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<Category> Categories { get; } = new();

        // This list holds the "Branches" (Batches) for the selected product
        public ObservableCollection<StockBatch> ProductBatches { get; } = new();

        // --- SELECTION & POPUP STATE ---
        private Product? _viewingProduct;
        public Product? ViewingProduct
        {
            get => _viewingProduct;
            set
            {
                _viewingProduct = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDetailVisible));

                if (value != null) LoadBatches(value.Id); // Load specific prices/history
            }
        }

        public bool IsDetailVisible => ViewingProduct != null;

        // --- SEARCH ---
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private Category? _selectedCategoryFilter;
        public Category? SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set { _selectedCategoryFilter = value; OnPropertyChanged(); ApplyFilters(); }
        }

        // --- COMMANDS ---
        public ICommand ClearSearchCommand { get; }
        public ICommand ViewProductCommand { get; }
        public ICommand CloseDetailCommand { get; }
        public ICommand EditProductCommand { get; }
        public ICommand DeleteProductCommand { get; }

        public ProductViewModel(IProductRepository productRepo, ICategoryRepository categoryRepo, IStockRepository stockRepo)
        {
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;
            _stockRepo = stockRepo; // Inject StockRepo

            ClearSearchCommand = new RelayCommand(ClearFilters);

            // Open the Popup
            ViewProductCommand = new RelayCommand<Product>(p => ViewingProduct = p);

            // Close the Popup
            CloseDetailCommand = new RelayCommand(() => ViewingProduct = null);

            // Edit (Placeholder for now)
            EditProductCommand = new RelayCommand<Product>(p =>
            {
                // We will hook up the Edit Popup logic here next!
                System.Windows.MessageBox.Show($"Edit Popup for {p.Name}");
            });

            // Delete
            DeleteProductCommand = new RelayCommand<Product>(async (p) => await DeleteProduct(p));

            LoadData();
        }

        private async void LoadData()
        {
            var cats = await _categoryRepo.GetAllAsync();
            Categories.Clear();
            Categories.Add(new Category { Id = -1, Name = "All Categories" });
            foreach (var c in cats) Categories.Add(c);
            SelectedCategoryFilter = Categories.First();

            var products = await _productRepo.GetAllAsync();
            _allProducts = products.ToList();

            ApplyFilters();
        }

        private async void LoadBatches(int productId)
        {
            ProductBatches.Clear();
            var allBatches = await _stockRepo.GetAllBatchesAsync(); // In real app, make a GetByProductId query!

            // Filter batches for this product
            foreach (var b in allBatches.Where(b => b.ProductId == productId))
            {
                ProductBatches.Add(b);
            }
        }

        private void ApplyFilters()
        {
            var query = _allProducts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
                query = query.Where(p => p.Name.ToLower().Contains(SearchText.ToLower()));

            if (SelectedCategoryFilter != null && SelectedCategoryFilter.Id != -1)
                query = query.Where(p => p.CategoryId == SelectedCategoryFilter.Id);

            Products.Clear();
            foreach (var p in query) Products.Add(p);
        }

        private void ClearFilters()
        {
            SearchText = "";
            SelectedCategoryFilter = Categories.FirstOrDefault();
            ApplyFilters();
        }

        private async Task DeleteProduct(Product p)
        {
            if (System.Windows.MessageBox.Show("Are you sure? This will delete all stock history too.", "Delete", System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes)
            {
                await _productRepo.DeleteAsync(p);
                ViewingProduct = null; // Close popup if open
                LoadData(); // Refresh list
            }
        }
    }
}