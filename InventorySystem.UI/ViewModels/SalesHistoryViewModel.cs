using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class SalesHistoryViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;
        public ObservableCollection<StockMovement> HistoryList { get; } = new();

        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-7);
        public DateTime EndDate { get; set; } = DateTime.Today;

        public ICommand FilterCommand { get; }

        public SalesHistoryViewModel(IStockRepository stockRepo)
        {
            _stockRepo = stockRepo;
            FilterCommand = new RelayCommand(async () => await LoadData());
            LoadData();
        }

        private async System.Threading.Tasks.Task LoadData()
        {
            HistoryList.Clear();
            var sales = await _stockRepo.GetSalesByDateRangeAsync(StartDate, EndDate.AddDays(1));
            foreach (var s in sales) HistoryList.Add(s);
        }
    }
}