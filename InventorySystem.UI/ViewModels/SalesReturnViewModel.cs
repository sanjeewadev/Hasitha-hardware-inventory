using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using InventorySystem.UI.Commands;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public enum ReturnReason { Restock, Defective }

    public class ReturnItemModel : ViewModelBase
    {
        public StockMovement OriginalMovement { get; }

        public decimal MaxReturnable => OriginalMovement.Quantity - OriginalMovement.ReturnedQuantity;
        public bool IsReturnable => MaxReturnable > 0;

        private decimal _returnQty;
        public decimal ReturnQty
        {
            get => _returnQty;
            set
            {
                if (value < 0) value = 0;
                if (value > MaxReturnable) value = MaxReturnable;

                _returnQty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RefundValue));
            }
        }

        private ReturnReason _reason = ReturnReason.Restock;
        public ReturnReason Reason
        {
            get => _reason;
            set { _reason = value; OnPropertyChanged(); }
        }

        public decimal RefundValue => ReturnQty * OriginalMovement.UnitPrice;

        public IEnumerable<ReturnReason> ReasonOptions => Enum.GetValues(typeof(ReturnReason)).Cast<ReturnReason>();

        public ReturnItemModel(StockMovement movement)
        {
            OriginalMovement = movement;
            ReturnQty = 0;
        }
    }

    public class SalesReturnViewModel : ViewModelBase
    {
        private readonly Data.Context.InventoryDbContext _context;

        private string _searchReceiptId = "";
        public string SearchReceiptId { get => _searchReceiptId; set { _searchReceiptId = value; OnPropertyChanged(); } }

        private SalesTransaction? _currentReceipt;
        public SalesTransaction? CurrentReceipt { get => _currentReceipt; set { _currentReceipt = value; OnPropertyChanged(); } }

        public ObservableCollection<ReturnItemModel> ReturnItems { get; } = new();

        public decimal TotalRefundAmount => ReturnItems.Sum(x => x.RefundValue);

        public ICommand SearchCommand { get; }
        public ICommand ProcessReturnCommand { get; }
        public ICommand ClearCommand { get; }

        public SalesReturnViewModel()
        {
            _context = Infrastructure.Services.DatabaseService.CreateDbContext();

            SearchCommand = new RelayCommand(async () => await SearchReceiptAsync());
            ProcessReturnCommand = new RelayCommand(async () => await ProcessReturnAsync());
            ClearCommand = new RelayCommand(ResetEntireForm); // Changed to ResetAll

            ReturnItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalRefundAmount));
        }

        private async Task SearchReceiptAsync()
        {
            // 1. Validate Input
            if (string.IsNullOrWhiteSpace(SearchReceiptId)) return;

            // 2. Capture the input BEFORE clearing anything
            string query = SearchReceiptId.Trim();

            // 3. Clear only the previous results (Keep the search text visible!)
            ResetResultsOnly();

            // 4. Try Exact Match First
            var receipt = await _context.SalesTransactions.FirstOrDefaultAsync(t => t.ReceiptId == query);

            // 5. If not found, try Partial Match
            if (receipt == null)
            {
                var candidates = await _context.SalesTransactions
                    .Where(t => t.ReceiptId.Contains(query))
                    .ToListAsync();

                if (candidates.Count == 1)
                {
                    receipt = candidates.First();
                }
                else if (candidates.Count > 1)
                {
                    MessageBox.Show($"Multiple receipts found matching '{query}'. Please enter the full ID.", "Multiple Matches", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            if (receipt == null)
            {
                MessageBox.Show("Receipt not found. Please check the ID.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Update UI with the Full ID found
            SearchReceiptId = receipt.ReceiptId;

            // Load Items
            var movements = await _context.StockMovements
                .Include(m => m.Product)
                .Where(m => m.ReceiptId == receipt.ReceiptId && m.Type == StockMovementType.Out && !m.IsVoided)
                .ToListAsync();

            if (!movements.Any())
            {
                MessageBox.Show("No valid items found on this receipt (or previously voided).", "Empty Receipt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CurrentReceipt = receipt;

            foreach (var move in movements)
            {
                var item = new ReturnItemModel(move);
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
                MessageBox.Show("Please enter a quantity to return.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var itemsToReturn = ReturnItems.Where(x => x.ReturnQty > 0).ToList();

            string message = $"Total Refund: Rs {TotalRefundAmount:N2}\n";
            if (CurrentReceipt.IsCredit && CurrentReceipt.Status == PaymentStatus.Unpaid)
            {
                message += "\n⚠ WARNING: This was a CREDIT SALE.\nDo NOT give cash. This will reduce their debt balance.";
            }
            else
            {
                message += "\nRefund Cash to Customer?";
            }

            if (MessageBox.Show(message, "Confirm Return", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                using var dbTrans = await _context.Database.BeginTransactionAsync();

                try
                {
                    foreach (var item in itemsToReturn)
                    {
                        var originalMove = item.OriginalMovement;

                        var returnMove = new StockMovement
                        {
                            ProductId = originalMove.ProductId,
                            StockBatchId = originalMove.StockBatchId,
                            Type = StockMovementType.SalesReturn,
                            Quantity = item.ReturnQty,
                            UnitCost = originalMove.UnitCost,
                            UnitPrice = originalMove.UnitPrice,
                            Date = DateTime.Now,
                            ReceiptId = CurrentReceipt.ReceiptId,
                            Note = $"Return ({item.Reason})"
                        };
                        _context.StockMovements.Add(returnMove);

                        var trackedMove = await _context.StockMovements.FindAsync(originalMove.Id);
                        if (trackedMove != null) trackedMove.ReturnedQuantity += item.ReturnQty;

                        if (item.Reason == ReturnReason.Restock)
                        {
                            var product = await _context.Products.FindAsync(originalMove.ProductId);
                            if (product != null) product.Quantity += item.ReturnQty;

                            if (originalMove.StockBatchId.HasValue)
                            {
                                var batch = await _context.StockBatches.FindAsync(originalMove.StockBatchId.Value);
                                if (batch != null) batch.RemainingQuantity += item.ReturnQty;
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await dbTrans.CommitAsync();

                    MessageBox.Show("Return processed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    ResetEntireForm(); // Clear everything on success
                }
                catch (Exception ex)
                {
                    await dbTrans.RollbackAsync();
                    MessageBox.Show($"Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Helper: Clear only the grid (Used when searching new ID)
        private void ResetResultsOnly()
        {
            CurrentReceipt = null;
            ReturnItems.Clear();
            OnPropertyChanged(nameof(TotalRefundAmount));
        }

        // Helper: Clear everything (Used by Cancel Button)
        private void ResetEntireForm()
        {
            SearchReceiptId = "";
            ResetResultsOnly();
        }
    }
}