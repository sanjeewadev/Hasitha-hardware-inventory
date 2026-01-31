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
    public class SalesHistoryViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;

        public ObservableCollection<SaleGroup> HistoryList { get; } = new();

        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-7);
        public DateTime EndDate { get; set; } = DateTime.Today;

        private bool _isDetailsVisible;
        public bool IsDetailsVisible { get => _isDetailsVisible; set { _isDetailsVisible = value; OnPropertyChanged(); } }

        private SaleGroup? _selectedSale;
        public SaleGroup? SelectedSale { get => _selectedSale; set { _selectedSale = value; OnPropertyChanged(); } }

        public ICommand FilterCommand { get; }
        public ICommand ClearFilterCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand CloseDetailsCommand { get; }

        public SalesHistoryViewModel(IStockRepository stockRepo)
        {
            _stockRepo = stockRepo;

            FilterCommand = new RelayCommand(async () => await LoadData());

            ClearFilterCommand = new RelayCommand(async () =>
            {
                StartDate = DateTime.Today.AddDays(-7);
                EndDate = DateTime.Today;
                OnPropertyChanged(nameof(StartDate));
                OnPropertyChanged(nameof(EndDate));
                await LoadData();
            });

            ViewDetailsCommand = new RelayCommand<SaleGroup>((sale) =>
            {
                SelectedSale = sale;
                IsDetailsVisible = true;
            });

            CloseDetailsCommand = new RelayCommand(() => IsDetailsVisible = false);

            LoadData();
        }

        private async Task LoadData()
        {
            HistoryList.Clear();
            var sales = await _stockRepo.GetSalesByDateRangeAsync(StartDate, EndDate.AddDays(1));

            // --- CRITICAL FIX: GROUP BY SECOND ---
            // We strip milliseconds by formatting to string "yyyyMMddHHmmss"
            // This forces items sold in the same second to merge into one receipt.
            var groupedSales = sales
                .Where(s => s.Type == Core.Enums.StockMovementType.Out)
                .GroupBy(s => s.Date.ToString("yyyyMMddHHmmss"))
                .Select(g => new SaleGroup(g.First().Date, g.ToList()))
                .OrderByDescending(g => g.Date);

            foreach (var item in groupedSales) HistoryList.Add(item);
        }
    }

    // Wrapper for the Main Grid Row
    public class SaleGroup
    {
        public DateTime Date { get; }
        public List<SaleItemDetail> Items { get; }

        // Grid Columns
        public string SummaryText => Items.Count == 1 ? Items.First().ProductName : $"{Items.Count} Items (Combined)";
        public int TotalQuantity => Items.Sum(i => i.Quantity);
        public decimal TotalAmount => Items.Sum(i => i.Total);

        // Generate a cleaner Ref ID based on time
        public string ReferenceId => Date.ToString("HHmmss");

        public SaleGroup(DateTime date, List<StockMovement> rawMovements)
        {
            Date = date;
            Items = rawMovements.Select(m => new SaleItemDetail
            {
                ProductName = m.Product?.Name ?? "Unknown",
                Barcode = m.Product?.Barcode ?? "-",
                Quantity = m.Quantity,
                UnitPrice = m.UnitPrice,
                Total = m.Quantity * m.UnitPrice,

                // For Popup Details
                Note = m.Note,
                UnitCost = m.UnitCost
            }).ToList();
        }

        // For Popup Footer
        public decimal EstimatedProfit => TotalAmount - Items.Sum(i => i.Quantity * i.UnitCost);
    }

    // Wrapper for the Inner Popup List
    public class SaleItemDetail
    {
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public decimal UnitCost { get; set; } // Hidden, for calculation
        public string Note { get; set; }
    }
}