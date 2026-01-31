using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
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

        // Grouped Collection for UI
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

            LoadData();
        }

        private async Task LoadData()
        {
            TodayTransactions.Clear();

            var start = DateTime.Today;
            var end = DateTime.Today.AddDays(1).AddTicks(-1);

            var allMoves = await _stockRepo.GetSalesByDateRangeAsync(start, end);

            // Filter: Active Sales Only (Not Voided)
            var activeSales = allMoves.Where(m => m.Type == Core.Enums.StockMovementType.Out && !m.IsVoided);

            // --- 1. CALCULATE DASHBOARD STATS ---
            DailyRevenue = activeSales.Sum(s => s.Quantity * s.UnitPrice);
            decimal totalCost = activeSales.Sum(s => s.Quantity * s.UnitCost);
            DailyProfit = DailyRevenue - totalCost;

            // --- 2. GROUP BY RECEIPT (Time) ---
            var grouped = activeSales
                .GroupBy(s => s.Date.ToString("yyyyMMddHHmmss"))
                .Select(g => new TodaySaleGroup(g.First().Date, g.ToList()))
                .OrderByDescending(g => g.Date)
                .ToList();

            SaleCount = grouped.Count;

            foreach (var group in grouped) TodayTransactions.Add(group);
        }

        private async Task ExecuteVoidLastSale()
        {
            // Get the most recent Sale Group
            var lastGroup = TodayTransactions.FirstOrDefault();

            if (lastGroup == null)
            {
                MessageBox.Show("No active sales found today to void.");
                return;
            }

            var result = MessageBox.Show(
                $"⚠ ARE YOU SURE?\n\nThis will DELETE the last sale:\n" +
                $"Time: {lastGroup.Date:hh:mm tt}\n" +
                $"Total: Rs {lastGroup.TotalAmount:N2}\n\n" +
                "Stock will be returned to inventory.",
                "Confirm Refund / Void",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Loop through all items in that receipt and void them
                foreach (var item in lastGroup.OriginalIds)
                {
                    await _stockRepo.VoidSaleAsync(item, "Undo Last Sale - Manager Override");
                }

                await LoadData();
                MessageBox.Show("Last Sale Voided Successfully.");
            }
        }
    }

    // --- WRAPPER CLASS (Mirror of History Wrapper) ---
    public class TodaySaleGroup
    {
        public DateTime Date { get; }
        public List<TodayItemDetail> Items { get; }
        public List<int> OriginalIds { get; } // To track IDs for Voiding

        public string ReferenceId => Date.ToString("HHmmss");
        public string SummaryText => Items.Count == 1 ? Items.First().ProductName : $"{Items.Count} Items";
        public int TotalItems => Items.Sum(i => i.Quantity);
        public decimal TotalAmount => Items.Sum(i => i.Total);

        public TodaySaleGroup(DateTime date, List<StockMovement> raw)
        {
            Date = date;
            OriginalIds = raw.Select(r => r.Id).ToList();
            Items = raw.Select(m => new TodayItemDetail
            {
                ProductName = m.Product?.Name ?? "?",
                Barcode = m.Product?.Barcode ?? "-",
                Quantity = m.Quantity,
                UnitPrice = m.UnitPrice,
                Total = m.Quantity * m.UnitPrice
            }).ToList();
        }
    }

    public class TodayItemDetail
    {
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }
}