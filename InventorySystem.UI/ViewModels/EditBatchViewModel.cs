using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class EditBatchViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;
        private readonly StockBatch _batch;

        public Action? CloseAction { get; set; }

        public int BatchId { get; }
        public string ProductName { get; }

        // --- PROPERTIES ---

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

        private decimal _sellingPrice;
        public decimal SellingPrice
        {
            get => _sellingPrice;
            set { _sellingPrice = value; OnPropertyChanged(); }
        }

        // --- FIX: Change from double to decimal ---
        private decimal _discount;
        public decimal Discount
        {
            get => _discount;
            set { _discount = value; OnPropertyChanged(); }
        }

        private string _discountCode = "";
        public string DiscountCode
        {
            get => _discountCode;
            set { _discountCode = value; OnPropertyChanged(); }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public EditBatchViewModel(IStockRepository stockRepo, StockBatch batch)
        {
            _stockRepo = stockRepo;
            _batch = batch;

            // Load Data
            BatchId = batch.Id;
            ProductName = batch.Product?.Name ?? "Unknown Product";
            Quantity = batch.RemainingQuantity;
            CostPrice = batch.CostPrice;
            SellingPrice = batch.SellingPrice;

            // Fix: No conversion needed now
            Discount = batch.Discount;

            DiscountCode = batch.DiscountCode;

            SaveCommand = new RelayCommand(async () => await SaveChanges());
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
        }

        private async Task SaveChanges()
        {
            if (Quantity < 0)
            {
                MessageBox.Show("Quantity cannot be negative.");
                return;
            }

            // Update the entity
            _batch.RemainingQuantity = Quantity;
            _batch.CostPrice = CostPrice;
            _batch.SellingPrice = SellingPrice;

            // Fix: No conversion needed now
            _batch.Discount = Discount;

            _batch.DiscountCode = DiscountCode;

            await _stockRepo.UpdateBatchAsync(_batch);

            MessageBox.Show("Batch updated successfully!");
            CloseAction?.Invoke();
        }
    }
}