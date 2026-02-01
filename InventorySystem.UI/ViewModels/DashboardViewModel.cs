using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows; // Added for MessageBox
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;
        private readonly IProductRepository _productRepo;

        // --- KPIS ---
        private decimal _periodRevenue;
        public decimal PeriodRevenue { get => _periodRevenue; set { _periodRevenue = value; OnPropertyChanged(); } }

        private decimal _periodProfit;
        public decimal PeriodProfit { get => _periodProfit; set { _periodProfit = value; OnPropertyChanged(); } }

        private int _transactionCount;
        public int TransactionCount { get => _transactionCount; set { _transactionCount = value; OnPropertyChanged(); } }

        private int _lowStockCount;
        public int LowStockCount { get => _lowStockCount; set { _lowStockCount = value; OnPropertyChanged(); } }

        // --- COLLECTIONS ---
        public ObservableCollection<Product> LowStockList { get; } = new();
        public ObservableCollection<ChartBar> WeeklySalesData { get; } = new();

        // --- FILTERS ---
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

        // --- COMMANDS ---
        public ICommand RefreshCommand { get; }
        public ICommand ClearFilterCommand { get; }

        public DashboardViewModel(IStockRepository sRepo, IProductRepository pRepo)
        {
            _stockRepo = sRepo;
            _productRepo = pRepo;

            RefreshCommand = new RelayCommand(async () => await LoadDashboardData());

            // Clear Filter Logic: Reset Dates to Today
            ClearFilterCommand = new RelayCommand(() =>
            {
                _startDate = DateTime.Today;
                _endDate = DateTime.Today;
                OnPropertyChanged(nameof(StartDate));
                OnPropertyChanged(nameof(EndDate));
                LoadDashboardData();
            });

            // Initial Load (Fire and forget, but handled internally)
            LoadDashboardData();
        }

        private async Task LoadDashboardData()
        {
            // 1. DATE VALIDATION
            if (StartDate > EndDate)
            {
                MessageBox.Show("Start Date cannot be after End Date.", "Invalid Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Stop loading to prevent weird calculations
            }

            try
            {
                DateTime actualStart = StartDate.Date;
                DateTime actualEnd = EndDate.Date.AddDays(1).AddTicks(-1);

                // 2. DATA FETCHING (Protected by Try-Catch)

                // Get Sales
                var fetchStart = actualEnd.AddDays(-7);
                if (actualStart < fetchStart) fetchStart = actualStart;

                var rawMoves = await _stockRepo.GetSalesByDateRangeAsync(fetchStart, actualEnd);

                // Filter valid sales only (ignore voided)
                var activeMoves = rawMoves.Where(m => !m.IsVoided).ToList();

                // Filter Strict Range for KPIs
                var rangeMoves = activeMoves.Where(m => m.Date >= actualStart && m.Date <= actualEnd).ToList();

                // Calculate KPIs
                PeriodRevenue = rangeMoves.Sum(m => m.Quantity * m.UnitPrice);
                decimal totalCost = rangeMoves.Sum(m => m.Quantity * m.UnitCost);
                PeriodProfit = PeriodRevenue - totalCost;

                TransactionCount = rangeMoves.Select(m => m.ReceiptId).Distinct().Count();

                // 3. LOW STOCK ALERTS
                var lowStockItems = await _stockRepo.GetLowStockProductsAsync(5);
                LowStockList.Clear();
                foreach (var p in lowStockItems) LowStockList.Add(p);

                LowStockCount = LowStockList.Count;

                // 4. BUILD CHART
                BuildChartData(activeMoves);
            }
            catch (Exception ex)
            {
                // CRASH PROTECTION: If DB fails, show message but keep app running
                MessageBox.Show($"Failed to load dashboard data.\n\nError: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildChartData(List<StockMovement> moves)
        {
            WeeklySalesData.Clear();

            // Filter moves strictly for chart display logic (last 7 days from EndDate)
            var relevantMoves = moves.Where(m => m.Date.Date <= EndDate.Date && m.Date.Date >= EndDate.Date.AddDays(-6)).ToList();

            var grouped = relevantMoves.GroupBy(m => m.Date.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity * x.UnitPrice));

            decimal maxVal = grouped.Values.DefaultIfEmpty(0).Max();
            if (maxVal == 0) maxVal = 1;

            for (int i = 6; i >= 0; i--)
            {
                var day = EndDate.Date.AddDays(-i);
                decimal val = grouped.ContainsKey(day) ? grouped[day] : 0;

                WeeklySalesData.Add(new ChartBar
                {
                    DayName = day.ToString("ddd"),
                    Value = val,
                    NormalizedHeight = (double)((val / maxVal) * 100),
                    Tooltip = $"{day:MMM dd}: Rs {val:N0}"
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
}