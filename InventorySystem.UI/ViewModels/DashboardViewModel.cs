using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System.Linq;

namespace InventorySystem.UI.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;

        public decimal TodaySales { get; set; }
        public int TransactionsToday { get; set; }
        public int LowStockItems { get; set; }

        public DashboardViewModel(IStockRepository stockRepo)
        {
            _stockRepo = stockRepo;
            LoadStats();
        }

        private async void LoadStats()
        {
            // 1. Get Sales
            var sales = await _stockRepo.GetSalesHistoryAsync(); // We will filter this in memory for V1
            var today = sales.Where(s => s.Date.Date == System.DateTime.Today && !s.IsVoided).ToList();

            // 2. Calculate Stats
            TransactionsToday = today.Count;
            // Note: Since we don't track "Price at Sale" in database fully yet, we estimate or use current price
            // Ideally, StockMovement should have a 'SalePrice' column. 
            // For now, let's just count quantity as a proxy or 0 if you prefer strictly accurate data.
            TodaySales = today.Sum(x => x.Quantity * 100); // Placeholder math

            // 3. Low Stock
            var lowStock = await _stockRepo.GetLowStockProductsAsync(5);
            LowStockItems = lowStock.Count();

            OnPropertyChanged(nameof(TodaySales));
            OnPropertyChanged(nameof(TransactionsToday));
            OnPropertyChanged(nameof(LowStockItems));
        }
    }
}