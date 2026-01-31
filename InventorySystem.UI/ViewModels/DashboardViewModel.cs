using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;
        private readonly IProductRepository _productRepo;

        // --- DATE RANGE FILTER ---
        private DateTime _startDate = DateTime.Today;
        public DateTime StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); LoadDashboardData(); }
        }

        private DateTime _endDate = DateTime.Today;
        public DateTime EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); LoadDashboardData(); }
        }

        // --- 1. KPI CARDS ---
        private decimal _periodRevenue;
        public decimal PeriodRevenue { get => _periodRevenue; set { _periodRevenue = value; OnPropertyChanged(); } }

        private decimal _periodProfit;
        public decimal PeriodProfit { get => _periodProfit; set { _periodProfit = value; OnPropertyChanged(); } }

        private int _transactionCount;
        public int TransactionCount { get => _transactionCount; set { _transactionCount = value; OnPropertyChanged(); } }

        private int _lowStockCount;
        public int LowStockCount { get => _lowStockCount; set { _lowStockCount = value; OnPropertyChanged(); } }

        // --- 2. CHART DATA ---
        public ObservableCollection<ChartBar> WeeklySalesData { get; } = new();

        // --- 3. HOT LISTS ---
        public ObservableCollection<TopProductItem> TopSellingProducts { get; } = new();
        public ObservableCollection<Product> LowStockList { get; } = new();

        public ICommand RefreshCommand { get; }

        public DashboardViewModel(IStockRepository stockRepo, IProductRepository productRepo)
        {
            _stockRepo = stockRepo;
            _productRepo = productRepo;

            RefreshCommand = new RelayCommand(async () => await LoadDashboardData());
            LoadDashboardData();
        }

        public async Task LoadDashboardData()
        {
            // 1. Fetch Data for the Broad Context (For Chart + Range)
            // We fetch from 7 days before the StartDate up to the EndDate to ensure the chart has data
            var chartStart = EndDate.AddDays(-6);
            var fetchStart = chartStart < StartDate ? chartStart : StartDate;
            var dayEnd = EndDate.Date.AddDays(1).AddTicks(-1);

            var rawMoves = await _stockRepo.GetSalesByDateRangeAsync(fetchStart, dayEnd);
            var activeMoves = rawMoves.Where(m => m.Type == Core.Enums.StockMovementType.Out && !m.IsVoided).ToList();

            // 2. Filter for KPI (The Selected Range Only)
            var rangeMoves = activeMoves.Where(m => m.Date >= StartDate && m.Date <= dayEnd).ToList();

            // --- CALCULATE KPI STATS ---
            PeriodRevenue = rangeMoves.Sum(m => m.Quantity * m.UnitPrice);
            var periodCost = rangeMoves.Sum(m => m.Quantity * m.UnitCost);
            PeriodProfit = PeriodRevenue - periodCost;

            TransactionCount = rangeMoves.GroupBy(m => m.Date.ToString("yyyyMMddHHmmss")).Count();

            // --- BUILD CHART (Trends ending at End Date) ---
            // Shows the last 7 days relative to the selection end
            BuildTrendChart(activeMoves);

            // --- BUILD TOP PRODUCTS (For Selected Range) ---
            BuildTopProductsList(rangeMoves);

            // --- LOW STOCK (Real-time snapshot) ---
            var lowStockItems = await _stockRepo.GetLowStockProductsAsync(10);
            LowStockList.Clear();
            foreach (var item in lowStockItems.Take(5)) LowStockList.Add(item);
            LowStockCount = lowStockItems.Count();
        }

        private void BuildTrendChart(List<StockMovement> moves)
        {
            WeeklySalesData.Clear();
            var salesByDate = moves
                .GroupBy(m => m.Date.Date)
                .ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity * m.UnitPrice));

            decimal maxSale = salesByDate.Values.DefaultIfEmpty(0).Max();
            if (maxSale == 0) maxSale = 1;

            // Show 7 days ending on the Selected END Date
            for (int i = 6; i >= 0; i--)
            {
                var date = EndDate.Date.AddDays(-i);
                decimal val = salesByDate.ContainsKey(date) ? salesByDate[date] : 0;

                WeeklySalesData.Add(new ChartBar
                {
                    DayName = date.ToString("ddd"), // "Mon", "Tue"
                    Value = val,
                    NormalizedHeight = (double)(val / maxSale) * 100,
                    Tooltip = $"{date:dd MMM}: Rs {val:N0}"
                });
            }
        }

        private void BuildTopProductsList(List<StockMovement> moves)
        {
            TopSellingProducts.Clear();
            if (!moves.Any()) return;

            var groups = moves
                .GroupBy(m => m.ProductId)
                .Select(g => new
                {
                    Name = g.First().Product?.Name ?? "Unknown",
                    Qty = g.Sum(m => m.Quantity),
                    Revenue = g.Sum(m => m.Quantity * m.UnitPrice)
                })
                .OrderByDescending(x => x.Qty)
                .Take(5)
                .ToList();

            int maxQty = groups.FirstOrDefault()?.Qty ?? 1;

            foreach (var g in groups)
            {
                TopSellingProducts.Add(new TopProductItem
                {
                    Name = g.Name,
                    Quantity = g.Qty,
                    Revenue = g.Revenue,
                    PopularityPercent = (double)g.Qty / maxQty * 100
                });
            }
        }
    }

    public class ChartBar
    {
        public string DayName { get; set; } = "";
        public decimal Value { get; set; }
        public double NormalizedHeight { get; set; }
        public string Tooltip { get; set; } = "";
    }

    public class TopProductItem
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
        public double PopularityPercent { get; set; }
    }
}