using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
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
        private readonly IStockRepository _stockRepo;

        // --- LEFT SIDE: SEARCH & SELECT ---
        private List<Product> _allProductsCache = new(); // Cache for fast search
        public ObservableCollection<Product> FilteredProducts { get; } = new();

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterList(); }
        }

        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                _selectedProduct = value;
                OnPropertyChanged();
                // When you click a product, auto-fill the Cost Price for convenience
                if (value != null) CostPrice = value.BuyingPrice;
            }
        }

        // --- RIGHT SIDE: INPUTS ---
        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set { _quantity = value; OnPropertyChanged(); }
        }

        private decimal _costPrice;
        public decimal CostPrice
        {
            get => _costPrice;
            set { _costPrice = value; OnPropertyChanged(); }
        }

        // Adjustment Inputs
        private int _adjustmentQty;
        public int AdjustmentQty
        {
            get => _adjustmentQty;
            set { _adjustmentQty = value; OnPropertyChanged(); }
        }

        private string _adjustmentReason = "Damaged";
        public string AdjustmentReason
        {
            get => _adjustmentReason;
            set { _adjustmentReason = value; OnPropertyChanged(); }
        }
        public ObservableCollection<string> AdjustmentReasons { get; } = new() { "Damaged", "Stolen", "Expired", "Inventory Correction" };

        // --- BOTTOM: HISTORY LOG ---
        public ObservableCollection<StockMovement> RecentHistory { get; } = new();

        // --- COMMANDS ---
        public ICommand ReceiveStockCommand { get; }
        public ICommand AdjustStockCommand { get; }

        public StockViewModel(IProductRepository productRepo, IStockRepository stockRepo)
        {
            _productRepo = productRepo;
            _stockRepo = stockRepo;

            ReceiveStockCommand = new RelayCommand(async () => await ReceiveStockAsync());
            AdjustStockCommand = new RelayCommand(async () => await AdjustStockAsync());

            LoadData();
        }

        private async void LoadData()
        {
            // Load Products
            var products = await _productRepo.GetAllAsync();
            _allProductsCache = products.ToList();
            FilterList();

            // Load Recent History
            await LoadHistory();
        }

        private void FilterList()
        {
            FilteredProducts.Clear();
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                foreach (var p in _allProductsCache) FilteredProducts.Add(p);
            }
            else
            {
                var lower = SearchText.ToLower();
                foreach (var p in _allProductsCache.Where(p => p.Name.ToLower().Contains(lower)))
                    FilteredProducts.Add(p);
            }
        }

        private async Task LoadHistory()
        {
            RecentHistory.Clear();
            var history = await _stockRepo.GetHistoryAsync(); // Assuming you have this method
            // Show only last 20 items for speed
            foreach (var h in history.OrderByDescending(x => x.Date).Take(20))
                RecentHistory.Add(h);
        }

        private async Task ReceiveStockAsync()
        {
            if (SelectedProduct == null) { MessageBox.Show("Select a product from the list!"); return; }
            if (Quantity <= 0) { MessageBox.Show("Quantity must be positive!"); return; }

            var movement = new StockMovement
            {
                ProductId = SelectedProduct.Id,
                Quantity = Quantity,
                Type = StockMovementType.In,
                Date = DateTime.UtcNow,
                // --- NEW: SAVE THE COST ---
                UnitCost = CostPrice,  // Save the specific cost of this batch
                UnitPrice = 0          // Not applicable for Stock In
            };

            // Note: We need to pass CostPrice to the repo too if we want to save it in a Batch
            // For now, we update the product's main buying price
            SelectedProduct.BuyingPrice = CostPrice;

            await _stockRepo.ReceiveStockAsync(movement);

            // Success
            MessageBox.Show($"Received {Quantity} x {SelectedProduct.Name}");
            Quantity = 0;

            LoadData(); // Refresh list and history
        }

        private async Task AdjustStockAsync()
        {
            if (SelectedProduct == null) { MessageBox.Show("Select a product!"); return; }
            if (AdjustmentQty <= 0) { MessageBox.Show("Quantity must be positive!"); return; }

            var adjustment = new StockMovement
            {
                ProductId = SelectedProduct.Id,
                Quantity = AdjustmentQty,
                Type = StockMovementType.Adjustment,
                Note = AdjustmentReason,
                Date = DateTime.UtcNow
            };

            await _stockRepo.AdjustStockAsync(adjustment);

            MessageBox.Show($"Removed {AdjustmentQty} x {SelectedProduct.Name}");
            AdjustmentQty = 0;

            LoadData();
        }
    }
}