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
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;
        // Removed ProductRepo as it was only needed for low stock

        // --- KPIS ---
        private decimal _periodRevenue;
        public decimal PeriodRevenue { get => _periodRevenue; set { _periodRevenue = value; OnPropertyChanged(); } }

        private decimal _periodProfit;
        public decimal PeriodProfit { get => _periodProfit; set { _periodProfit = value; OnPropertyChanged(); } }

        private int _transactionCount;
        public int TransactionCount { get => _transactionCount; set { _transactionCount = value; OnPropertyChanged(); } }

        // --- COLLECTIONS ---
        public ObservableCollection<ChartBar> WeeklySalesData { get; } = new();

        // --- FILTERS ---
        // Default: Last 7 Days
        private DateTime _startDate = DateTime.Today.AddDays(-6);
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

        public ICommand RefreshCommand { get; }
        public ICommand ClearFilterCommand { get; }

        public DashboardViewModel(IStockRepository sRepo)
        {
            _stockRepo = sRepo;

            RefreshCommand = new RelayCommand(async () => await LoadDashboardData());

            ClearFilterCommand = new RelayCommand(() =>
            {
                StartDate = DateTime.Today.AddDays(-6);
                EndDate = DateTime.Today;
            });

            _ = LoadDashboardData();
        }

        private async Task LoadDashboardData()
        {
            if (StartDate > EndDate)
            {
                MessageBox.Show("Start Date cannot be after End Date.", "Invalid Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                DateTime actualStart = StartDate.Date;
                DateTime actualEnd = EndDate.Date.AddDays(1).AddTicks(-1);

                // 1. SALES DATA
                // Fetch context for chart history (padding start date if needed)
                var rawMoves = await _stockRepo.GetSalesByDateRangeAsync(actualStart.AddDays(-1), actualEnd);
                var activeMoves = rawMoves.Where(m => !m.IsVoided).ToList();

                // Filter strictly for KPI calculation
                var rangeMoves = activeMoves.Where(m => m.Date >= actualStart && m.Date <= actualEnd).ToList();

                // 2. CALCULATE KPIS
                PeriodRevenue = rangeMoves.Sum(m => m.Quantity * m.UnitPrice);
                decimal totalCost = rangeMoves.Sum(m => m.Quantity * m.UnitCost);
                PeriodProfit = PeriodRevenue - totalCost;
                TransactionCount = rangeMoves.Select(m => m.ReceiptId).Distinct().Count();

                // 3. BUILD CHART
                BuildChartData(activeMoves);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load dashboard data.\n\nError: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildChartData(List<StockMovement> moves)
        {
            WeeklySalesData.Clear();

            var chartEndDate = EndDate.Date;
            // Generate chart bars for the selected duration (or max 7 days for readability)
            // Here we stick to the last 7 days ending at EndDate

            var relevantMoves = moves.Where(m => m.Date.Date <= chartEndDate).ToList();
            var grouped = relevantMoves.GroupBy(m => m.Date.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity * x.UnitPrice));

            decimal maxVal = grouped.Values.DefaultIfEmpty(0).Max();
            if (maxVal == 0) maxVal = 1000;

            for (int i = 6; i >= 0; i--)
            {
                var day = chartEndDate.AddDays(-i);
                decimal val = grouped.ContainsKey(day) ? grouped[day] : 0;

                // Dynamic Bar Height (Max 250px now since we have more vertical space)
                double pixelHeight = (double)((val / maxVal) * 200);
                if (pixelHeight < 5) pixelHeight = 5;

                WeeklySalesData.Add(new ChartBar
                {
                    DayName = day.ToString("ddd"),
                    Value = val,
                    NormalizedHeight = pixelHeight,
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