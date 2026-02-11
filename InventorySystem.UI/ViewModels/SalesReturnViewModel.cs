using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using InventorySystem.UI.Commands;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    // A temporary wrapper to hold the UI state for each item being returned
    public class ReturnItemModel : ViewModelBase
    {
        public StockMovement OriginalMovement { get; }

        public decimal MaxReturnable => OriginalMovement.Quantity - OriginalMovement.ReturnedQuantity;

        private decimal _returnQty;
        public decimal ReturnQty
        {
            get => _returnQty;
            set
            {
                // Prevent typing more than they are allowed to return
                if (value < 0) value = 0;
                if (value > MaxReturnable) value = MaxReturnable;

                _returnQty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RefundValue));
            }
        }

        public decimal RefundValue => ReturnQty * OriginalMovement.UnitPrice;

        public ReturnItemModel(StockMovement movement)
        {
            OriginalMovement = movement;
            ReturnQty = 0;
        }
    }

    public class SalesReturnViewModel : ViewModelBase
    {
        private readonly Data.Context.InventoryDbContext _context;

        // --- SEARCH ---
        private string _searchReceiptId = "";
        public string SearchReceiptId { get => _searchReceiptId; set { _searchReceiptId = value; OnPropertyChanged(); } }

        // --- STATE ---
        private SalesTransaction? _currentReceipt;
        public SalesTransaction? CurrentReceipt { get => _currentReceipt; set { _currentReceipt = value; OnPropertyChanged(); } }

        public ObservableCollection<ReturnItemModel> ReturnItems { get; } = new();

        public decimal TotalRefundAmount => ReturnItems.Sum(x => x.RefundValue);

        // --- COMMANDS ---
        public ICommand SearchCommand { get; }
        public ICommand ProcessReturnCommand { get; }
        public ICommand ClearCommand { get; }

        public SalesReturnViewModel()
        {
            _context = Infrastructure.Services.DatabaseService.CreateDbContext();

            SearchCommand = new RelayCommand(async () => await SearchReceiptAsync());
            ProcessReturnCommand = new RelayCommand(async () => await ProcessReturnAsync());
            ClearCommand = new RelayCommand(ClearForm);

            // Listen for changes inside the collection to update the Total Refund Amount in real-time
            ReturnItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalRefundAmount));
        }

        private async Task SearchReceiptAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchReceiptId)) return;

            ClearForm();
            SearchReceiptId = SearchReceiptId.Trim(); // Restore search text after clear

            // Fetch Receipt and its Outward Stock Movements
            var receipt = await _context.SalesTransactions.FirstOrDefaultAsync(t => t.ReceiptId == SearchReceiptId);

            if (receipt == null)
            {
                MessageBox.Show("Receipt not found. Please check the ID.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var movements = await _context.StockMovements
                .Include(m => m.Product)
                .Where(m => m.ReceiptId == SearchReceiptId && m.Type == StockMovementType.Out && !m.IsVoided)
                .ToListAsync();

            if (!movements.Any())
            {
                MessageBox.Show("No valid items found on this receipt.", "Empty Receipt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CurrentReceipt = receipt;

            foreach (var move in movements)
            {
                var item = new ReturnItemModel(move);
                // Subscribe to child changes so the Total updates instantly when typing
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ReturnItemModel.RefundValue))
                        OnPropertyChanged(nameof(TotalRefundAmount));
                };
                ReturnItems.Add(item);
            }
        }

        private async Task ProcessReturnAsync()
        {
            if (CurrentReceipt == null || !ReturnItems.Any(x => x.ReturnQty > 0))
            {
                MessageBox.Show("Please enter a quantity to return for at least one item.", "No Items Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var itemsToReturn = ReturnItems.Where(x => x.ReturnQty > 0).ToList();

            if (MessageBox.Show($"Process refund for Rs {TotalRefundAmount:N2}?\nThis will securely return the items to inventory.", "Confirm Return", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                using var dbTrans = await _context.Database.BeginTransactionAsync();

                try
                {
                    foreach (var item in itemsToReturn)
                    {
                        var originalMove = item.OriginalMovement;

                        // 1. Log the Return Movement
                        var returnMove = new StockMovement
                        {
                            ProductId = originalMove.ProductId,
                            StockBatchId = originalMove.StockBatchId, // Locks it back to exact batch!
                            Type = StockMovementType.SalesReturn,
                            Quantity = item.ReturnQty,
                            UnitCost = originalMove.UnitCost,
                            UnitPrice = originalMove.UnitPrice,
                            Date = DateTime.Now,
                            ReceiptId = CurrentReceipt.ReceiptId, // Link to original receipt
                            Note = $"Customer Return (Ref: {CurrentReceipt.ReceiptId})"
                        };
                        _context.StockMovements.Add(returnMove);

                        // 2. Update Original Movement's tracking to prevent Double-Dipping
                        var trackedMove = await _context.StockMovements.FindAsync(originalMove.Id);
                        if (trackedMove != null) trackedMove.ReturnedQuantity += item.ReturnQty;

                        // 3. Restore Global Product Stock
                        var product = await _context.Products.FindAsync(originalMove.ProductId);
                        if (product != null) product.Quantity += item.ReturnQty;

                        // 4. Restore Batch Stock (Crucial for profit margins)
                        if (originalMove.StockBatchId.HasValue)
                        {
                            var batch = await _context.StockBatches.FindAsync(originalMove.StockBatchId.Value);
                            if (batch != null) batch.RemainingQuantity += item.ReturnQty;
                        }
                    }

                    // Optional: If you want to deduct from the day's SalesTransaction total, you can do it here. 
                    // However, standard accounting leaves the original receipt intact and uses the SalesReturn movements to calculate net sales.

                    await _context.SaveChangesAsync();
                    await dbTrans.CommitAsync();

                    MessageBox.Show("Return processed successfully. Stock has been restored.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearForm();
                }
                catch (Exception ex)
                {
                    await dbTrans.RollbackAsync();
                    MessageBox.Show($"Failed to process return: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearForm()
        {
            SearchReceiptId = "";
            CurrentReceipt = null;
            ReturnItems.Clear();
            OnPropertyChanged(nameof(TotalRefundAmount));
        }
    }
}