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

        // Read-Only Info
        public string Title => $"Edit Batch ({_batch.ReceivedDate:dd MMM yyyy})";
        public int Quantity => _batch.RemainingQuantity;

        // Editable Fields
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public double Discount { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public EditBatchViewModel(IStockRepository stockRepo, StockBatch batch)
        {
            _stockRepo = stockRepo;
            _batch = batch;

            // Load current values
            CostPrice = batch.CostPrice;
            SellingPrice = batch.SellingPrice;
            Discount = batch.Discount;

            SaveCommand = new RelayCommand(async () => await SaveAsync());
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
        }

        private async Task SaveAsync()
        {
            // Update the batch object
            _batch.CostPrice = CostPrice;
            _batch.SellingPrice = SellingPrice;
            _batch.Discount = Discount;

            // Save to DB
            await _stockRepo.UpdateBatchAsync(_batch); // We need to add this method to Repo

            CloseAction?.Invoke();
        }
    }
}