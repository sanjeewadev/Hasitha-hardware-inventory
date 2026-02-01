using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows; // Required for MessageBox
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

        public SalesHistoryViewModel(IStockRepository stockRepo)
        {
            _stockRepo = stockRepo;

            SearchCommand = new RelayCommand(async () => await ExecuteSearch());

            ResetFilterCommand = new RelayCommand(() =>
            {
                StartDate = DateTime.Today.AddDays(-7);
                EndDate = DateTime.Today;
                ExecuteSearch();
            });

            ViewDetailsCommand = new RelayCommand<SalesHistoryItem>(item => SelectedSale = item);
            CloseDetailsCommand = new RelayCommand(() => SelectedSale = null);

            // Load initial data
            ExecuteSearch();
        }

        private async Task ExecuteSearch()
        {
            // VALIDATION 1: Date Range Check
            if (StartDate > EndDate)
            {
                MessageBox.Show("Start Date cannot be after End Date.", "Invalid Date Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Force Start to 00:00:00
                DateTime actualStart = StartDate.Date;
                // Force End to 23:59:59
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
                            UnitPrice = x.UnitPrice
                        }).ToList()
                    })
                    .OrderByDescending(x => x.Date)
                    .ToList();

                SalesHistory.Clear();

                if (groupedSales.Any())
                {
                    foreach (var sale in groupedSales) SalesHistory.Add(sale);
                }
                else
                {
                    // VALIDATION 2: Empty State Feedback (Optional but helpful)
                    // We don't necessarily need a popup here, but if the user specifically clicked "Search", it's good to know.
                    // If this was an auto-load, maybe skip the message. 
                    // For now, we leave it silent or you can uncomment the line below:
                    // MessageBox.Show("No sales found for this period.", "No Records", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // VALIDATION 3: Crash Protection
                MessageBox.Show($"Failed to load sales history.\n\nError: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // --- DTO CLASSES ---
    public class SalesHistoryItem
    {
        public string ReferenceId { get; set; } = "";
        public DateTime Date { get; set; }
        public bool IsVoided { get; set; }
        public int TotalItems { get; set; }
        public decimal TotalAmount { get; set; }
        public List<SaleDetailItem> Items { get; set; } = new();

        public string SummaryText
        {
            get
            {
                if (IsVoided) return "[VOIDED / REFUNDED]";
                if (Items.Count == 1) return Items[0].ProductName;
                return $"{Items.Count} Items (Combined)";
            }
        }
    }

    public class SaleDetailItem
    {
        public string ProductName { get; set; } = "";
        public string Barcode { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total => Quantity * UnitPrice;
    }
}