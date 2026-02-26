using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
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
    public class SalesHistoryViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;
        private List<SalesHistoryItem> _allHistoryCache = new();

        private DateTime _startDate = DateTime.Today.AddDays(-7);
        public DateTime StartDate { get => _startDate; set { _startDate = value; OnPropertyChanged(); } }

        private DateTime _endDate = DateTime.Today;
        public DateTime EndDate { get => _endDate; set { _endDate = value; OnPropertyChanged(); } }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterHistory(); }
        }

        public ObservableCollection<SalesHistoryItem> SalesHistory { get; } = new();

        private SalesHistoryItem? _selectedSale;
        public SalesHistoryItem? SelectedSale
        {
            get => _selectedSale;
            set { _selectedSale = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsDetailsVisible)); }
        }

        public bool IsDetailsVisible => SelectedSale != null;

        public ICommand SearchCommand { get; }
        public ICommand ResetFilterCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand CloseDetailsCommand { get; }
        public ICommand PrintReceiptCommand { get; }
        public ICommand CopyIdCommand { get; } // NEW: Added Copy Command

        public SalesHistoryViewModel(IStockRepository stockRepo)
        {
            _stockRepo = stockRepo;

            SearchCommand = new RelayCommand(async () => await ExecuteSearch());

            ResetFilterCommand = new RelayCommand(() =>
            {
                StartDate = DateTime.Today.AddDays(-7);
                EndDate = DateTime.Today;
                SearchText = "";
                _ = ExecuteSearch();
            });

            ViewDetailsCommand = new RelayCommand<SalesHistoryItem>(item => SelectedSale = item);
            CloseDetailsCommand = new RelayCommand(() => SelectedSale = null);

            PrintReceiptCommand = new RelayCommand(PrintCurrentReceipt);

            // NEW: Implementation for Copy ID
            CopyIdCommand = new RelayCommand<string>((id) =>
            {
                if (!string.IsNullOrEmpty(id))
                {
                    Clipboard.SetText(id);
                    MessageBox.Show($"Receipt ID '{id}' copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });

            _ = ExecuteSearch();
        }

        private async Task ExecuteSearch()
        {
            if (StartDate > EndDate)
            {
                MessageBox.Show("Start Date cannot be after End Date.", "Invalid Date Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                DateTime actualStart = StartDate.Date;
                DateTime actualEnd = EndDate.Date.AddDays(1).AddTicks(-1);

                var allMoves = await _stockRepo.GetSalesByDateRangeAsync(actualStart, actualEnd);

                var validMoves = allMoves.Where(m => !m.IsVoided).ToList();

                var receiptIds = validMoves.Select(m => m.ReceiptId).Distinct().ToList();
                var transactions = await _stockRepo.GetTransactionsByReceiptIdsAsync(receiptIds);

                var groupedSales = validMoves
                    .GroupBy(m => m.ReceiptId)
                    .Select(g =>
                    {
                        var outs = g.Where(x => x.Type == StockMovementType.Out).ToList();
                        var returns = g.Where(x => x.Type == StockMovementType.SalesReturn).ToList();
                        var tx = transactions.FirstOrDefault(t => t.ReceiptId == g.Key);

                        return new SalesHistoryItem(tx)
                        {
                            ReferenceId = g.Key,
                            Date = outs.FirstOrDefault()?.Date ?? g.First().Date,
                            TotalItems = outs.Sum(x => x.Quantity) - returns.Sum(x => x.Quantity),
                            TotalAmount = outs.Sum(x => x.Quantity * x.UnitPrice) - returns.Sum(x => x.Quantity * x.UnitPrice),

                            Items = outs.Select(x => new SaleDetailItem
                            {
                                ProductName = x.Product?.Name ?? "Unknown",
                                Barcode = x.Product?.Barcode ?? "-",
                                Quantity = x.Quantity,
                                Unit = x.Product?.Unit ?? "",
                                UnitPrice = x.UnitPrice
                            }).ToList()
                        };
                    })
                    .Where(s => s.TotalItems > 0)
                    .OrderByDescending(x => x.Date)
                    .ToList();

                _allHistoryCache = groupedSales;
                FilterHistory();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load sales history.\n\nError: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterHistory()
        {
            SalesHistory.Clear();
            var query = _allHistoryCache.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lower = SearchText.ToLower();
                query = query.Where(s =>
                    s.ReferenceId.ToLower().Contains(lower) ||
                    s.Items.Any(i => i.ProductName.ToLower().Contains(lower))
                );
            }

            foreach (var sale in query) SalesHistory.Add(sale);
        }

        private void PrintCurrentReceipt()
        {
            if (SelectedSale == null) return;
            try
            {
                string printerName = Properties.Settings.Default.PrinterName;
                int copies = Properties.Settings.Default.ReceiptCopies;

                string receiptText = $"HISTORICAL RECEIPT\nDate: {SelectedSale.Date}\nRef: {SelectedSale.ReferenceId}\n----------------\n";
                foreach (var item in SelectedSale.Items)
                {
                    receiptText += $"{item.ProductName} x{item.Quantity}  {item.Total:N2}\n";
                }
                receiptText += $"----------------\nTotal: {SelectedSale.TotalAmount:N2}\n\n(Reprinted Copy)";

                var printService = new PrintService();
                printService.PrintReceipt(SelectedSale.ReferenceId, receiptText, printerName, copies);
                MessageBox.Show("Sent to printer.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print Failed: {ex.Message}");
            }
        }
    }

    public class SalesHistoryItem
    {
        public string ReferenceId { get; set; } = "";
        public DateTime Date { get; set; }
        public decimal TotalItems { get; set; }
        public decimal TotalAmount { get; set; }
        public List<SaleDetailItem> Items { get; set; } = new();
        public string StatusDisplay { get; }

        public SalesHistoryItem(SalesTransaction? tx)
        {
            if (tx == null) StatusDisplay = "Unknown";
            else if (!tx.IsCredit) StatusDisplay = "💰 CASH - PAID";
            else if (tx.Status == PaymentStatus.Paid) StatusDisplay = "💳 CREDIT - SETTLED";
            else StatusDisplay = $"⏳ CREDIT - DUE (Rs {tx.RemainingBalance:N0})";
        }

        public string SummaryText => Items.Count == 1 ? Items[0].ProductName : $"{Items.Count} Items (Combined)";
    }

    public class SaleDetailItem
    {
        public string ProductName { get; set; } = "";
        public string Barcode { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal Total => Quantity * UnitPrice;
    }
}