using InventorySystem.Core.Entities;
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
    public class TodaySalesViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;
        private List<TodaySaleGroup> _allSalesCache = new();

        public ObservableCollection<TodaySaleGroup> TodayTransactions { get; } = new();

        // --- DASHBOARD STATS ---
        private decimal _dailyRevenue;
        public decimal DailyRevenue { get => _dailyRevenue; set { _dailyRevenue = value; OnPropertyChanged(); } }

        private decimal _dailyProfit;
        public decimal DailyProfit { get => _dailyProfit; set { _dailyProfit = value; OnPropertyChanged(); } }

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
        public ICommand CopyIdCommand { get; } // <--- NEW

        public TodaySalesViewModel(IStockRepository stockRepo)
        {
            _stockRepo = stockRepo;

            RefreshCommand = new RelayCommand(async () => await LoadData());
            DeleteSaleCommand = new RelayCommand<TodaySaleGroup>(async (sale) => await ExecuteDeleteSale(sale));

            ViewDetailsCommand = new RelayCommand<TodaySaleGroup>((sale) => {
                SelectedSale = sale;
                IsDetailsVisible = true;
            });

            CloseDetailsCommand = new RelayCommand(() => IsDetailsVisible = false);
            PrintReceiptCommand = new RelayCommand(PrintCurrentReceipt);

            // NEW: Copy to Clipboard Logic
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
                var activeSales = allMoves.Where(m => m.Type == Core.Enums.StockMovementType.Out && !m.IsVoided);

                // 1. STATS
                DailyRevenue = activeSales.Sum(s => s.Quantity * s.UnitPrice);
                decimal totalCost = activeSales.Sum(s => s.Quantity * s.UnitCost);
                DailyProfit = DailyRevenue - totalCost;

                // 2. GROUPING
                var grouped = activeSales
                    .GroupBy(s => s.ReceiptId)
                    .Select(g => new TodaySaleGroup(g.First().Date, g.ToList()))
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

            string warningMsg = $"⚠ SECURITY WARNING: VOID TRANSACTION\n\n" +
                                $"Receipt: {sale.ReferenceId}\n" +
                                $"Amount: Rs {sale.TotalAmount:N2}\n\n" +
                                $"This will remove revenue and restore items to stock.\n" +
                                $"Are you sure?";

            if (MessageBox.Show(warningMsg, "Confirm Void", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
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
                MessageBox.Show("Sent to printer.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print Failed: {ex.Message}");
            }
        }
    }

    public class TodaySaleGroup
    {
        public DateTime Date { get; }
        public List<TodayItemDetail> Items { get; }
        public string ReferenceId { get; }

        public string SummaryText => Items.Count == 1 ? Items.First().ProductName : $"{Items.Count} Items";
        public decimal TotalItems => Items.Sum(i => i.Quantity);
        public decimal TotalAmount => Items.Sum(i => i.Total);
        public string CustomerDisplay { get; }

        public TodaySaleGroup(DateTime date, List<StockMovement> raw)
        {
            Date = date;
            ReferenceId = raw.First().ReceiptId;
            string note = raw.First().Note;
            CustomerDisplay = string.IsNullOrWhiteSpace(note) ? "Walk-in" : note;

            Items = raw.Select(m => new TodayItemDetail
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