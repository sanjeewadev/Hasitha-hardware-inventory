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
        private readonly Action _recalculateCallback;
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
                _recalculateCallback?.Invoke();
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

        public ReturnItemModel(StockMovement movement, Action recalculateCallback)
        {
            OriginalMovement = movement;
            _recalculateCallback = recalculateCallback;
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

        private decimal _cashToRefund;
        public decimal CashToRefund { get => _cashToRefund; set { _cashToRefund = value; OnPropertyChanged(); } }

        private decimal _debtReduced;
        public decimal DebtReduced { get => _debtReduced; set { _debtReduced = value; OnPropertyChanged(); } }

        private string _refundActionText = "Select items to return";
        public string RefundActionText { get => _refundActionText; set { _refundActionText = value; OnPropertyChanged(); } }

        private string _refundActionColor = "#1E293B";
        public string RefundActionColor { get => _refundActionColor; set { _refundActionColor = value; OnPropertyChanged(); } }

        public ICommand SearchCommand { get; }
        public ICommand ProcessReturnCommand { get; }
        public ICommand ClearCommand { get; }

        public SalesReturnViewModel()
        {
            _context = Infrastructure.Services.DatabaseService.CreateDbContext();

            SearchCommand = new RelayCommand(async () => await SearchReceiptAsync());
            ProcessReturnCommand = new RelayCommand(async () => await ProcessReturnAsync());
            ClearCommand = new RelayCommand(ResetEntireForm);
        }

        private async Task SearchReceiptAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchReceiptId)) return;

            string query = SearchReceiptId.Trim();
            ResetResultsOnly();

            var receipt = await _context.SalesTransactions.FirstOrDefaultAsync(t => t.ReceiptId == query);

            if (receipt == null)
            {
                var candidates = await _context.SalesTransactions.Where(t => t.ReceiptId.Contains(query)).ToListAsync();
                if (candidates.Count == 1) receipt = candidates.First();
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

            SearchReceiptId = receipt.ReceiptId;

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
                ReturnItems.Add(new ReturnItemModel(move, RecalculateRefundAction));
            }
            RecalculateRefundAction();
        }

        private void RecalculateRefundAction()
        {
            OnPropertyChanged(nameof(TotalRefundAmount));

            if (CurrentReceipt == null || TotalRefundAmount == 0)
            {
                CashToRefund = 0; DebtReduced = 0;
                RefundActionText = "Select items to return";
                RefundActionColor = "#1E293B";
                return;
            }

            if (!CurrentReceipt.IsCredit)
            {
                CashToRefund = TotalRefundAmount;
                DebtReduced = 0;
                RefundActionText = $"HAND CASH TO CUSTOMER: Rs {CashToRefund:N2}";
                RefundActionColor = "#10B981";
            }
            else
            {
                decimal currentDebt = CurrentReceipt.TotalAmount - CurrentReceipt.PaidAmount;

                if (TotalRefundAmount <= currentDebt)
                {
                    CashToRefund = 0;
                    DebtReduced = TotalRefundAmount;
                    RefundActionText = $"DEBT REDUCED BY Rs {DebtReduced:N2} (DO NOT GIVE CASH)";
                    RefundActionColor = "#F59E0B";
                }
                else
                {
                    DebtReduced = currentDebt;
                    CashToRefund = TotalRefundAmount - currentDebt;
                    RefundActionText = $"DEBT CLEARED. HAND CASH: Rs {CashToRefund:N2}";
                    RefundActionColor = "#3B82F6";
                }
            }
        }

        private async Task ProcessReturnAsync()
        {
            if (CurrentReceipt == null || !ReturnItems.Any(x => x.ReturnQty > 0))
            {
                MessageBox.Show("Please enter a quantity to return.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string confirmMsg = $"{RefundActionText}\n\nProceed with processing this return?";

            if (MessageBox.Show(confirmMsg, "Confirm Return", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                using var dbTrans = await _context.Database.BeginTransactionAsync();

                try
                {
                    var transaction = await _context.SalesTransactions.FirstOrDefaultAsync(t => t.ReceiptId == CurrentReceipt.ReceiptId);

                    var itemsToReturn = ReturnItems.Where(x => x.ReturnQty > 0).ToList();
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

                    // --- THE LEDGER FIX ---
                    if (transaction != null)
                    {
                        transaction.TotalAmount -= TotalRefundAmount;

                        if (CashToRefund > 0)
                        {
                            // Reduce the 'PaidAmount' pool so the transaction balances
                            transaction.PaidAmount -= CashToRefund;

                            // NEW FIX: If we hand cash back on a credit sale, log it as a negative payment!
                            // This guarantees the Cash Drawer math on the Today's Sales page drops accurately.
                            if (transaction.IsCredit)
                            {
                                var refundLog = new CreditPaymentLog
                                {
                                    ReceiptId = transaction.ReceiptId,
                                    AmountPaid = -CashToRefund, // Negative value because cash is LEAVING
                                    PaymentDate = DateTime.Now,
                                    Note = "Cash Refund (Sales Return)"
                                };
                                _context.CreditPaymentLogs.Add(refundLog);
                            }
                        }

                        if (transaction.TotalAmount < 0) transaction.TotalAmount = 0;
                        if (transaction.PaidAmount < 0) transaction.PaidAmount = 0;

                        if (transaction.TotalAmount <= transaction.PaidAmount)
                            transaction.Status = PaymentStatus.Paid;
                        else if (transaction.PaidAmount > 0)
                            transaction.Status = PaymentStatus.PartiallyPaid;
                        else
                            transaction.Status = PaymentStatus.Unpaid;

                        _context.SalesTransactions.Update(transaction);
                    }

                    await _context.SaveChangesAsync();
                    await dbTrans.CommitAsync();

                    MessageBox.Show("Return processed successfully.\nFinancials and Inventory have been updated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    ResetEntireForm();
                }
                catch (Exception ex)
                {
                    await dbTrans.RollbackAsync();
                    MessageBox.Show($"Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ResetResultsOnly()
        {
            CurrentReceipt = null;
            ReturnItems.Clear();
            RecalculateRefundAction();
        }

        private void ResetEntireForm()
        {
            SearchReceiptId = "";
            ResetResultsOnly();
        }
    }
}