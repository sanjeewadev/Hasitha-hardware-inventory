using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace InventorySystem.UI.ViewModels
{
    public class ReportsViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;

        // --- KPI Cards ---
        private decimal _totalSales;
        public decimal TotalSales
        {
            get => _totalSales;
            set { _totalSales = value; OnPropertyChanged(); }
        }

        private decimal _totalProfit;
        public decimal TotalProfit
        {
            get => _totalProfit;
            set { _totalProfit = value; OnPropertyChanged(); }
        }

        private int _totalItemsSold;
        public int TotalItemsSold
        {
            get => _totalItemsSold;
            set { _totalItemsSold = value; OnPropertyChanged(); }
        }

        // --- Lists ---
        public ObservableCollection<StockMovement> RecentSales { get; } = new();
        public ObservableCollection<Product> LowStockItems { get; } = new();

        public ReportsViewModel(IStockRepository stockRepo)
        {
            _stockRepo = stockRepo;
            LoadReportData();
        }

        private async void LoadReportData()
        {
            // 1. Fetch Sales History
            var sales = await _stockRepo.GetSalesHistoryAsync();

            RecentSales.Clear();
            foreach (var s in sales) RecentSales.Add(s);

            // 2. Calculate KPIs
            TotalItemsSold = sales.Sum(s => s.Quantity);

            // Simple Logic: Sales = Qty * Current Selling Price
            TotalSales = sales.Sum(s => s.Quantity * s.Product.SellingPrice);

            // Simple Profit: (Selling - Buying) * Qty
            TotalProfit = sales.Sum(s => s.Quantity * (s.Product.SellingPrice - s.Product.BuyingPrice));

            // 3. Fetch Low Stock (Threshold = 5 items)
            var lowStock = await _stockRepo.GetLowStockProductsAsync(5);
            LowStockItems.Clear();
            foreach (var p in lowStock) LowStockItems.Add(p);
        }
    }
}