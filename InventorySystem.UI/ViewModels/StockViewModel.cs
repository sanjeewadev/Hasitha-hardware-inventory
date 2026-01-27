using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System;
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

        // --- Inputs ---
        public ObservableCollection<Product> Products { get; } = new();

        // NEW: List for the table
        public ObservableCollection<StockBatch> StockHistory { get; } = new();

        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                _selectedProduct = value;
                OnPropertyChanged();
                // Auto-fill cost with current market price for convenience
                if (value != null) CostPrice = value.BuyingPrice;
            }
        }

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

        // --- Adjustment Inputs ---
        private int _adjustmentQty;
        public int AdjustmentQty
        {
            get => _adjustmentQty;
            set { _adjustmentQty = value; OnPropertyChanged(); }
        }

        private string _adjustmentReason = "Damaged"; // Default
        public string AdjustmentReason
        {
            get => _adjustmentReason;
            set { _adjustmentReason = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> AdjustmentReasons { get; } = new()
        {
            "Damaged", "Stolen", "Expired", "Inventory Correction"
        };

        public ICommand AdjustStockCommand { get; }

        // --- Command ---
        public ICommand ReceiveStockCommand { get; }

        public StockViewModel(IProductRepository productRepo, IStockRepository stockRepo)
        {
            _productRepo = productRepo;
            _stockRepo = stockRepo;

            LoadProducts();
            LoadHistory(); // <--- Call this!

            ReceiveStockCommand = new RelayCommand(async () => await ReceiveStockAsync());
            AdjustStockCommand = new RelayCommand(async () => await AdjustStockAsync());
        }

        private async Task AdjustStockAsync()
        {
            if (SelectedProduct == null) { MessageBox.Show("Select a product!"); return; }
            if (AdjustmentQty <= 0) { MessageBox.Show("Quantity must be positive!"); return; }

            var adjustment = new StockMovement
            {
                ProductId = SelectedProduct.Id,
                Quantity = AdjustmentQty,
                Type = Core.Enums.StockMovementType.Adjustment, // Make sure you have this Enum value!
                Note = AdjustmentReason, // Store the reason here
                Date = DateTime.UtcNow
            };

            await _stockRepo.AdjustStockAsync(adjustment);

            MessageBox.Show($"Stock adjusted: -{AdjustmentQty} {SelectedProduct.Name}");

            // Clear inputs
            AdjustmentQty = 0;
            SelectedProduct = null;

            // Refresh
            LoadProducts();
            LoadHistory();
        }

        private void LoadProducts()
        {
            Products.Clear();
            var list = _productRepo.GetAllAsync().Result; // Simple load
            foreach (var p in list) Products.Add(p);
        }

        private async void LoadHistory()
        {
            StockHistory.Clear();
            var batches = await _stockRepo.GetAllBatchesAsync();
            foreach (var b in batches) StockHistory.Add(b);
        }

        private async Task ReceiveStockAsync()
        {
            if (SelectedProduct == null) { MessageBox.Show("Select a product!"); return; }
            if (Quantity <= 0) { MessageBox.Show("Quantity must be positive!"); return; }
            if (CostPrice <= 0) { MessageBox.Show("Cost must be positive!"); return; }

            var batch = new StockBatch
            {
                ProductId = SelectedProduct.Id,
                InitialQuantity = Quantity,
                RemainingQuantity = Quantity, // Initially full
                CostPrice = CostPrice,
                ReceivedDate = DateTime.UtcNow
            };

            await _stockRepo.ReceiveStockAsync(batch);

            MessageBox.Show($"Received {Quantity} {SelectedProduct.Name}(s) successfully!");

            // Reset Form
            Quantity = 0;
            CostPrice = 0;
            SelectedProduct = null;

            // Refresh product list to show new Quantity
            LoadProducts();
            LoadHistory();
        }
    }
}