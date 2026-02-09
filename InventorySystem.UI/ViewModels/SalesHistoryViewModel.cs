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
    public class SalesHistoryViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;

        // --- FILTERS ---
        private DateTime _startDate = DateTime.Today.AddDays(-7);
        public DateTime StartDate { get => _startDate; set { _startDate = value; OnPropertyChanged(); } }

        private DateTime _endDate = DateTime.Today;
        public DateTime EndDate { get => _endDate; set { _endDate = value; OnPropertyChanged(); } }

        // --- LIST DATA ---
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
        public ICommand PrintReceiptCommand { get; } // <--- NEW

        public SalesHistoryViewModel(IStockRepository stockRepo)
        {
            _stockRepo = stockRepo;

            SearchCommand = new RelayCommand(async () => await ExecuteSearch());

            ResetFilterCommand = new RelayCommand(() =>
            {
                StartDate = DateTime.Today.AddDays(-7);
                EndDate = DateTime.Today;
                _ = ExecuteSearch();
            });

            ViewDetailsCommand = new RelayCommand<SalesHistoryItem>(item => SelectedSale = item);
            CloseDetailsCommand = new RelayCommand(() => SelectedSale = null);

            // New Print Command
            PrintReceiptCommand = new RelayCommand(PrintCurrentReceipt);

            // Load initial data
            _ = ExecuteSearch();
        }

        private void PrintCurrentReceipt()
        {
            if (SelectedSale == null) return;

            try
            {
                string printerName = Properties.Settings.Default.PrinterName;

                string receiptText = $"HISTORICAL RECEIPT\nDate: {SelectedSale.Date}\nRef: {SelectedSale.ReferenceId}\n----------------\n";
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
                MessageBox.Show($"Print Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

                var groupedSales = allMoves
                    .Where(m => m.Type == Core.Enums.StockMovementType.Out)
                    .GroupBy(m => m.ReceiptId)
                    .Select(g => new SalesHistoryItem
                    {
                        ReferenceId = g.Key,
                        Date = g.First().Date,
                        IsVoided = g.Any(x => x.IsVoided),
                        TotalItems = g.Sum(x => x.Quantity),
                        TotalAmount = g.Sum(x => x.Quantity * x.UnitPrice),
                        Items = g.Select(x => new SaleDetailItem
                        {
                            ProductName = x.Product?.Name ?? "Unknown",
                            Barcode = x.Product?.Barcode ?? "-",
                            Quantity = x.Quantity,
                            Unit = x.Product?.Unit ?? "",
                            UnitPrice = x.UnitPrice
                        }).ToList()
                    })
                    .OrderByDescending(x => x.Date)
                    .ToList();

                SalesHistory.Clear();
                foreach (var sale in groupedSales) SalesHistory.Add(sale);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load sales history.\n\nError: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // --- (Keep DTOs same as before) ---
    public class SalesHistoryItem
    {
        public string ReferenceId { get; set; } = "";
        public DateTime Date { get; set; }
        public bool IsVoided { get; set; }
        public decimal TotalItems { get; set; }
        public decimal TotalAmount { get; set; }
        public List<SaleDetailItem> Items { get; set; } = new();

        public string SummaryText
        {
            get
            {
                if (IsVoided) return "[VOIDED / REFUNDED]";
                if (Items.Count == 1) return Items[0].ProductName;
                return $"{TotalItems:0.###} Items (Combined)";
            }
        }
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