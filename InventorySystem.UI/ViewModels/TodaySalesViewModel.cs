using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class TodaySalesViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;
        public ObservableCollection<StockMovement> TodaySales { get; } = new();
        public ICommand VoidSaleCommand { get; }

        public TodaySalesViewModel(IStockRepository stockRepo)
        {
            _stockRepo = stockRepo;
            VoidSaleCommand = new RelayCommand<StockMovement>(async (s) => await VoidSale(s));
            LoadData();
        }

        private async void LoadData()
        {
            TodaySales.Clear();
            var sales = await _stockRepo.GetSalesByDateRangeAsync(DateTime.Today, DateTime.Today.AddDays(1));
            foreach (var s in sales) TodaySales.Add(s);
        }

        private async System.Threading.Tasks.Task VoidSale(StockMovement sale)
        {
            if (sale == null || sale.IsVoided) return;
            if (MessageBox.Show("Void this sale and restore stock?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No) return;

            await _stockRepo.VoidSaleAsync(sale.Id, "Manual Void");
            LoadData(); // Refresh
        }
    }
}