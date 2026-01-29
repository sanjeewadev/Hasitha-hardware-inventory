using InventorySystem.Core.Entities;
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

        // --- LEFT PANEL: NAVIGATION ---
        public ObservableCollection<Category> CategoryTree { get; } = new();
        public ObservableCollection<Product> ProductsInSelectedCategory { get; } = new();
        private List<Product> _allProductsCache = new();

        private Category? _selectedCategory;
        public Category? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                OnPropertyChanged();
                SearchText = ""; // Clear search on folder change
                LoadProductsForCategory();
            }
        }

        // Search Logic
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterProducts();
            }
        }

        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                _selectedProduct = value;
                OnPropertyChanged();

                if (_selectedProduct != null)
                {
                    // PRE-FILL inputs with current product data
                    StockInCost = _selectedProduct.BuyingPrice;
                    StockInSellingPrice = _selectedProduct.SellingPrice; // <--- Pre-fill Selling Price
                    StockInDiscount = _selectedProduct.DiscountLimit;    // <--- Pre-fill Discount
                    LoadBatchHistory(_selectedProduct.Id);
                }
                else
                {
                    ClearInputs();
                }
            }
        }

        // --- RIGHT PANEL: STOCK IN INPUTS ---
        public DateTime StockInDate { get; set; } = DateTime.Now;

        private int _stockInQty;
        public int StockInQty
        {
            get => _stockInQty;
            set { _stockInQty = value; OnPropertyChanged(); }
        }

        private decimal _stockInCost;
        public decimal StockInCost
        {
            get => _stockInCost;
            set
            {
                _stockInCost = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeneratedSecretCode));
            }
        }

        // NEW: Selling Price
        private decimal _stockInSellingPrice;
        public decimal StockInSellingPrice
        {
            get => _stockInSellingPrice;
            set { _stockInSellingPrice = value; OnPropertyChanged(); }
        }

        // NEW: Discount
        private double _stockInDiscount;
        public double StockInDiscount
        {
            get => _stockInDiscount;
            set { _stockInDiscount = value; OnPropertyChanged(); }
        }

        public string GeneratedSecretCode => GenerateCipher(StockInCost);

        // --- RIGHT PANEL: HISTORY ---
        public ObservableCollection<StockBatch> BatchHistory { get; } = new();

        // --- COMMANDS ---
        public ICommand StockInCommand { get; }

        public StockViewModel(IProductRepository pRepo, ICategoryRepository cRepo, IStockRepository sRepo)
        {
            _productRepo = pRepo;
            _categoryRepo = cRepo;
            _stockRepo = sRepo;

            StockInCommand = new RelayCommand(async () => await ExecuteStockIn());

            LoadTree();
        }

        private async void LoadTree()
        {
            var all = await _categoryRepo.GetAllAsync();
            var cats = all.ToList();

            CategoryTree.Clear();
            foreach (var c in cats) c.SubCategories.Clear();
            foreach (var c in cats)
            {
                if (c.ParentId != null)
                    cats.FirstOrDefault(p => p.Id == c.ParentId)?.SubCategories.Add(c);
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

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                foreach (var p in _allProductsCache) ProductsInSelectedCategory.Add(p);
            }
            else
            {
                var lower = SearchText.ToLower();
                var filtered = _allProductsCache.Where(p =>
                    p.Name.ToLower().Contains(lower) ||
                    p.Barcode.ToLower().Contains(lower));

                foreach (var p in filtered) ProductsInSelectedCategory.Add(p);
            }
        }

        private async void LoadBatchHistory(int productId)
        {
            BatchHistory.Clear();
            var batches = await _stockRepo.GetAllBatchesAsync();
            var relevant = batches
                .Where(b => b.ProductId == productId)
                .OrderByDescending(b => b.ReceivedDate);

            foreach (var b in relevant) BatchHistory.Add(b);
        }

        private async Task ExecuteStockIn()
        {
            if (SelectedProduct == null) return;
            if (StockInQty <= 0) { MessageBox.Show("Quantity must be greater than 0"); return; }

            // 1. Create Batch (History)
            var batch = new StockBatch
            {
                ProductId = SelectedProduct.Id,
                InitialQuantity = StockInQty,
                RemainingQuantity = StockInQty,
                CostPrice = StockInCost,
                ReceivedDate = StockInDate
            };

            await _stockRepo.AddStockBatchAsync(batch);

            // 2. Update Product (Owner Decision)
            SelectedProduct.Quantity += StockInQty;
            SelectedProduct.BuyingPrice = StockInCost;       // Update Reference Cost
            SelectedProduct.SellingPrice = StockInSellingPrice; // Update Shelf Price
            SelectedProduct.DiscountLimit = StockInDiscount;    // Update Discount Rule

            await _productRepo.UpdateAsync(SelectedProduct);

            MessageBox.Show("Stock Added & Prices Updated!");

            ClearInputs();
            LoadBatchHistory(SelectedProduct.Id);

            // Refresh List Logic (Preserve Search)
            var currentSearch = SearchText;
            LoadProductsForCategory();
            SearchText = currentSearch;
        }

        private void ClearInputs()
        {
            StockInQty = 0;
            StockInCost = 0;
            StockInSellingPrice = 0;
            StockInDiscount = 0;
            StockInDate = DateTime.Now;
            OnPropertyChanged(nameof(StockInQty));
            OnPropertyChanged(nameof(StockInCost));
            OnPropertyChanged(nameof(StockInSellingPrice));
            OnPropertyChanged(nameof(StockInDiscount));
            OnPropertyChanged(nameof(StockInDate));
        }

        private string GenerateCipher(decimal price)
        {
            if (price == 0) return "-";
            return $"CIPHER: {price:00}";
        }
    }
}