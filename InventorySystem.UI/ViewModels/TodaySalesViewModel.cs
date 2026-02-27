using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
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
    public class TodaySalesViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;
        private readonly Data.Context.InventoryDbContext _dbContext;
        private List<TodaySaleGroup> _allSalesCache = new();

        public ObservableCollection<TodaySaleGroup> TodayTransactions { get; } = new();

        // --- DASHBOARD STATS ---
        private decimal _dailyRevenue;
        public decimal DailyRevenue { get => _dailyRevenue; set { _dailyRevenue = value; OnPropertyChanged(); } }

        // FIX: Added DailyProfit back!
        private decimal _dailyProfit;
        public decimal DailyProfit { get => _dailyProfit; set { _dailyProfit = value; OnPropertyChanged(); } }

        // Real physical cash tracking
        private decimal _actualCashInDrawer;
        public decimal ActualCashInDrawer { get => _actualCashInDrawer; set { _actualCashInDrawer = value; OnPropertyChanged(); } }

        private decimal _debtCollectedToday;
        public decimal DebtCollectedToday { get => _debtCollectedToday; set { _debtCollectedToday = value; OnPropertyChanged(); } }

        private int _saleCount;
        public int SaleCount { get => _saleCount; set { _saleCount = value; OnPropertyChanged(); } }

        // --- SEARCH ---
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterSales(); }
        }

        // --- POPUP STATE ---
        private bool _isDetailsVisible;
        public bool IsDetailsVisible { get => _isDetailsVisible; set { _isDetailsVisible = value; OnPropertyChanged(); } }

        private TodaySaleGroup? _selectedSale;
        public TodaySaleGroup? SelectedSale { get => _selectedSale; set { _selectedSale = value; OnPropertyChanged(); } }

        // --- COMMANDS ---
        public ICommand RefreshCommand { get; }
        public ICommand DeleteSaleCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand CloseDetailsCommand { get; }
        public ICommand PrintReceiptCommand { get; }
        public ICommand CopyIdCommand { get; }

        public TodaySalesViewModel(IStockRepository stockRepo)
        {
            // CRITICAL FIX: Ignore the global repo! Create a fresh private channel to the DB.
            _dbContext = Infrastructure.Services.DatabaseService.CreateDbContext();
            _stockRepo = new StockRepository(_dbContext);

            RefreshCommand = new RelayCommand(async () => await LoadData());
            DeleteSaleCommand = new RelayCommand<TodaySaleGroup>(async (sale) => await ExecuteDeleteSale(sale));

            ViewDetailsCommand = new RelayCommand<TodaySaleGroup>((sale) => {
                SelectedSale = sale;
                IsDetailsVisible = true;
            });

            CloseDetailsCommand = new RelayCommand(() => IsDetailsVisible = false);
            PrintReceiptCommand = new RelayCommand(PrintCurrentReceipt);

            CopyIdCommand = new RelayCommand<string>((id) =>
            {
                if (!string.IsNullOrEmpty(id))
                {
                    Clipboard.SetText(id);
                    MessageBox.Show($"Receipt ID '{id}' copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });

            LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                var start = DateTime.Today;
                var end = DateTime.Today.AddDays(1).AddTicks(-1);

                var allMoves = await _stockRepo.GetSalesByDateRangeAsync(start, end);

                var validMoves = allMoves.Where(m => !m.IsVoided &&
                    (m.Type == StockMovementType.Out || m.Type == StockMovementType.SalesReturn || m.Type == StockMovementType.Adjustment)).ToList();

                var validSales = validMoves.Where(m => m.Type == StockMovementType.Out).ToList();
                var validReturns = validMoves.Where(m => m.Type == StockMovementType.SalesReturn).ToList();
                var validAdjustments = validMoves.Where(m => m.Type == StockMovementType.Adjustment).ToList();

                var receiptIds = validSales.Concat(validReturns).Select(s => s.ReceiptId).Distinct().ToList();
                var transactions = await _stockRepo.GetTransactionsByReceiptIdsAsync(receiptIds);

                // --- 1. REVENUE CALCULATION ---
                decimal grossRevenue = validSales.Sum(s => s.Quantity * s.UnitPrice);
                decimal returnRevenue = validReturns.Sum(r => r.Quantity * r.UnitPrice);
                DailyRevenue = grossRevenue - returnRevenue;

                // --- 2. PROFIT CALCULATION (Including Losses) ---
                decimal grossCost = validSales.Sum(s => s.Quantity * s.UnitCost);
                decimal returnCost = validReturns.Sum(r => r.Quantity * r.UnitCost);
                decimal lossCost = validAdjustments.Sum(a => a.Quantity * a.UnitCost); // Lost items cost

                DailyProfit = DailyRevenue - (grossCost - returnCost) - lossCost;

                // --- 3. EXACT CASH DRAWER CALCULATION ---
                decimal cashSalesToday = validSales
                    .Where(m => transactions.Any(t => t.ReceiptId == m.ReceiptId && !t.IsCredit))
                    .Sum(m => m.Quantity * m.UnitPrice);

                decimal cashRefundsToday = validReturns
                    .Where(m => transactions.Any(t => t.ReceiptId == m.ReceiptId && !t.IsCredit))
                    .Sum(m => m.Quantity * m.UnitPrice);

                DebtCollectedToday = await _dbContext.CreditPaymentLogs
                    .Where(log => log.PaymentDate >= start && log.PaymentDate <= end)
                    .SumAsync(log => log.AmountPaid);

                ActualCashInDrawer = cashSalesToday + DebtCollectedToday - cashRefundsToday;

                // --- 4. GROUPING (Exclude adjustments from the visual list) ---
                var grouped = validMoves
                    .Where(m => m.Type != StockMovementType.Adjustment)
                    .GroupBy(s => s.ReceiptId)
                    .Select(g =>
                    {
                        var outs = g.Where(x => x.Type == StockMovementType.Out).ToList();
                        var returns = g.Where(x => x.Type == StockMovementType.SalesReturn).ToList();
                        var tx = transactions.FirstOrDefault(t => t.ReceiptId == g.Key);

                        return new TodaySaleGroup(g.First().Date, outs, returns, tx);
                    })
                    .OrderByDescending(g => g.Date)
                    .ToList();

                SaleCount = grouped.Count;

                _allSalesCache = grouped;
                FilterSales();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterSales()
        {
            TodayTransactions.Clear();
            var query = _allSalesCache.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lower = SearchText.ToLower();
                query = query.Where(s => s.ReferenceId.ToLower().Contains(lower) || s.SummaryText.ToLower().Contains(lower));
            }

            foreach (var s in query) TodayTransactions.Add(s);
        }

        private async Task ExecuteDeleteSale(TodaySaleGroup sale)
        {
            if (sale == null) return;

            string msg1 = $"⚠ SECURITY WARNING: VOID TRANSACTION\n\n" +
                          $"Receipt: {sale.ReferenceId}\nAmount: Rs {sale.TotalAmount:N2}\n\n" +
                          $"Are you sure you want to void this sale?";

            if (MessageBox.Show(msg1, "Confirm Void (1/2)", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                string msg2 = $"🚨 FINAL WARNING 🚨\n\nAre you REALLY sure you want to delete this sale?\n" +
                              $"This will reverse the revenue and restore the items to stock. This cannot be undone.";

                if (MessageBox.Show(msg2, "Confirm Void (2/2)", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _stockRepo.VoidReceiptAsync(sale.ReferenceId);
                        await LoadData();
                        IsDetailsVisible = false;
                        MessageBox.Show("Transaction Voided Successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Void Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void PrintCurrentReceipt()
        {
            if (SelectedSale == null) return;
            try
            {
                string printerName = Properties.Settings.Default.PrinterName;
                string receiptText = $"REPRINT RECEIPT\nDate: {SelectedSale.Date}\nRef: {SelectedSale.ReferenceId}\n----------------\n";
                foreach (var item in SelectedSale.Items)
                {
                    receiptText += $"{item.ProductName} x{item.Quantity}  {item.Total:N2}\n";
                }
                receiptText += $"----------------\nTotal: {SelectedSale.TotalAmount:N2}\n\n(Reprinted Copy)";

                var printService = new PrintService();
                printService.PrintReceipt(SelectedSale.ReferenceId, receiptText, printerName, 1);
            }
            catch (Exception ex) { MessageBox.Show($"Print Failed: {ex.Message}"); }
        }
    }

    public class TodaySaleGroup
    {
        public DateTime Date { get; }
        public List<TodayItemDetail> Items { get; }
        public string ReferenceId { get; }
        public string SummaryText => Items.Count == 1 ? Items.First().ProductName : $"{Items.Count} Items";
        public decimal TotalItems { get; }
        public decimal TotalAmount { get; }
        public string CustomerDisplay { get; }
        public string StatusDisplay { get; }

        public TodaySaleGroup(DateTime date, List<StockMovement> outs, List<StockMovement> returns, SalesTransaction? tx)
        {
            Date = date;
            var firstMove = outs.FirstOrDefault() ?? returns.FirstOrDefault();
            ReferenceId = firstMove?.ReceiptId ?? "Unknown";
            CustomerDisplay = string.IsNullOrWhiteSpace(firstMove?.Note) ? "Walk-in" : firstMove.Note;

            TotalItems = outs.Sum(i => i.Quantity) - returns.Sum(i => i.Quantity);
            TotalAmount = outs.Sum(i => i.Quantity * i.UnitPrice) - returns.Sum(i => i.Quantity * i.UnitPrice);

            if (TotalItems <= 0 && returns.Any()) StatusDisplay = "🔄 FULLY RETURNED";
            else if (returns.Any()) StatusDisplay = $"🔄 PARTIAL RETURN";
            else if (tx == null) StatusDisplay = "Unknown / Voided";
            else if (!tx.IsCredit) StatusDisplay = "💰 CASH - PAID";
            else if (tx.Status == PaymentStatus.Paid) StatusDisplay = "💳 CREDIT - SETTLED";
            else StatusDisplay = $"⏳ CREDIT - DUE (Rs {tx.RemainingBalance:N0})";

            Items = outs.Select(m => new TodayItemDetail
            {
                ProductName = m.Product?.Name ?? "?",
                Barcode = m.Product?.Barcode ?? "-",
                Quantity = m.Quantity,
                Unit = m.Product?.Unit ?? "",
                UnitPrice = m.UnitPrice,
                Total = m.Quantity * m.UnitPrice,
                ReceiptId = m.ReceiptId
            }).ToList();
        }
    }

    public class TodayItemDetail
    {
        public string ProductName { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public string ReceiptId { get; set; } = string.Empty;
    }
}