using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services; // For PrintService
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

        public ObservableCollection<TodaySaleGroup> TodayTransactions { get; } = new();

        // --- DASHBOARD STATS ---
        private decimal _dailyRevenue;
        public decimal DailyRevenue { get => _dailyRevenue; set { _dailyRevenue = value; OnPropertyChanged(); } }

        private decimal _dailyProfit;
        public decimal DailyProfit { get => _dailyProfit; set { _dailyProfit = value; OnPropertyChanged(); } }

        private int _saleCount;
        public int SaleCount { get => _saleCount; set { _saleCount = value; OnPropertyChanged(); } }

        // --- POPUP STATE ---
        private bool _isDetailsVisible;
        public bool IsDetailsVisible { get => _isDetailsVisible; set { _isDetailsVisible = value; OnPropertyChanged(); } }

        private TodaySaleGroup? _selectedSale;
        public TodaySaleGroup? SelectedSale { get => _selectedSale; set { _selectedSale = value; OnPropertyChanged(); } }

        // --- COMMANDS ---
        public ICommand RefreshCommand { get; }
        public ICommand VoidLastSaleCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand CloseDetailsCommand { get; }
        public ICommand PrintReceiptCommand { get; } // <--- NEW

        public TodaySalesViewModel(IStockRepository stockRepo)
        {
            _stockRepo = stockRepo;

            RefreshCommand = new RelayCommand(async () => await LoadData());
            VoidLastSaleCommand = new RelayCommand(async () => await ExecuteVoidLastSale());

            ViewDetailsCommand = new RelayCommand<TodaySaleGroup>((sale) => {
                SelectedSale = sale;
                IsDetailsVisible = true;
            });

            CloseDetailsCommand = new RelayCommand(() => IsDetailsVisible = false);

            // New Print Command
            PrintReceiptCommand = new RelayCommand(PrintCurrentReceipt);

            LoadData();
        }

        private void PrintCurrentReceipt()
        {
            if (SelectedSale == null) return;

            try
            {
                // 1. Get Printer Name (Ignore Count, Force 1)
                string printerName = Properties.Settings.Default.PrinterName;

                // 2. Build Receipt Content
                string receiptText = $"REPRINT RECEIPT\nDate: {SelectedSale.Date}\nRef: {SelectedSale.ReferenceId}\n----------------\n";

                foreach (var item in SelectedSale.Items)
                {
                    receiptText += $"{item.ProductName} x{item.Quantity}  {item.Total:N2}\n";
                }

                receiptText += $"----------------\nTotal: {SelectedSale.TotalAmount:N2}\n\n(Reprinted Copy)";

                // 3. Print (Force 1 copy)
                var printService = new PrintService();
                printService.PrintReceipt(SelectedSale.ReferenceId, receiptText, printerName, 1);

                MessageBox.Show("Sent to printer.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadData()
        {
            try
            {
                TodayTransactions.Clear();
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

                foreach (var group in grouped) TodayTransactions.Add(group);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load today's sales data.\n\nError: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteVoidLastSale()
        {
            var lastGroup = TodayTransactions.FirstOrDefault();
            if (lastGroup == null)
            {
                MessageBox.Show("No active sales found today to void.", "List Empty", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string warningMsg = $"⚠ SECURITY WARNING: VOIDING TRANSACTION\n\n" +
                                $"You are about to undo the last sale:\n" +
                                $"--------------------------------------\n" +
                                $"Time: {lastGroup.Date:hh:mm tt}\n" +
                                $"Items: {lastGroup.TotalItems}\n" +
                                $"Total Refund: Rs {lastGroup.TotalAmount:N2}\n" +
                                $"--------------------------------------\n\n" +
                                $"• Revenue will be deducted.\n" +
                                $"• Items will be returned to Stock.\n\n" +
                                $"Are you sure you want to proceed?";

            if (MessageBox.Show(warningMsg, "Confirm Refund / Void", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    var receiptId = lastGroup.Items.First().ReceiptId;
                    await _stockRepo.VoidReceiptAsync(receiptId);
                    await LoadData();
                    MessageBox.Show("Transaction Voided Successfully.\nStock has been restored.", "Void Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not void transaction.\n\nError: {ex.Message}", "Void Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    // --- (Keep TodaySaleGroup and TodayItemDetail classes same as before) ---
    public class TodaySaleGroup
    {
        public DateTime Date { get; }
        public List<TodayItemDetail> Items { get; }
        public string ReferenceId => Date.ToString("HHmmss");
        public string SummaryText => Items.Count == 1 ? Items.First().ProductName : $"{Items.Count} Items";
        public decimal TotalItems => Items.Sum(i => i.Quantity);
        public decimal TotalAmount => Items.Sum(i => i.Total);

        public TodaySaleGroup(DateTime date, List<StockMovement> raw)
        {
            Date = date;
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