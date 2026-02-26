using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
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
    public class AuditLogViewModel : ViewModelBase
    {
        private readonly IStockRepository _stockRepo;
        private List<AuditLogItem> _allAuditCache = new();

        private DateTime _startDate = DateTime.Today.AddDays(-30);
        public DateTime StartDate { get => _startDate; set { _startDate = value; OnPropertyChanged(); } }

        private DateTime _endDate = DateTime.Today;
        public DateTime EndDate { get => _endDate; set { _endDate = value; OnPropertyChanged(); } }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterData(); }
        }

        public ObservableCollection<AuditLogItem> AuditLogs { get; } = new();

        private AuditLogItem? _selectedLog;
        public AuditLogItem? SelectedLog
        {
            get => _selectedLog;
            set { _selectedLog = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsDetailsVisible)); }
        }

        public bool IsDetailsVisible => SelectedLog != null;

        public ICommand SearchCommand { get; }
        public ICommand ResetFilterCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand CloseDetailsCommand { get; }

        public AuditLogViewModel(IStockRepository stockRepo)
        {
            _stockRepo = stockRepo;

            SearchCommand = new RelayCommand(async () => await LoadData());
            ResetFilterCommand = new RelayCommand(() =>
            {
                StartDate = DateTime.Today.AddDays(-30);
                EndDate = DateTime.Today;
                SearchText = "";
                _ = LoadData();
            });

            ViewDetailsCommand = new RelayCommand<AuditLogItem>(item => SelectedLog = item);
            CloseDetailsCommand = new RelayCommand(() => SelectedLog = null);

            _ = LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                DateTime actualStart = StartDate.Date;
                DateTime actualEnd = EndDate.Date.AddDays(1).AddTicks(-1);

                var rawData = await _stockRepo.GetVoidsAndReturnsAsync(actualStart, actualEnd);

                // Group by ReceiptId AND Action Type (Return vs Void)
                var grouped = rawData
                    .GroupBy(m => new { m.ReceiptId, IsReturn = m.Type == StockMovementType.SalesReturn })
                    .Select(g => new AuditLogItem
                    {
                        ReceiptId = g.Key.ReceiptId,
                        Date = g.First().Date,
                        ActionType = g.Key.IsReturn ? "RETURN" : "VOID",
                        TotalItems = g.Sum(x => x.Quantity),
                        TotalValue = g.Sum(x => x.Quantity * x.UnitPrice),
                        Items = g.Select(x => new AuditDetailItem
                        {
                            ProductName = x.Product?.Name ?? "Unknown",
                            Quantity = x.Quantity,
                            UnitPrice = x.UnitPrice,
                            Reason = string.IsNullOrWhiteSpace(x.Note) ? "No reason provided" : x.Note
                        }).ToList()
                    })
                    .OrderByDescending(x => x.Date)
                    .ToList();

                _allAuditCache = grouped;
                FilterData();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading audit log: {ex.Message}");
            }
        }

        private void FilterData()
        {
            AuditLogs.Clear();
            var query = _allAuditCache.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lower = SearchText.ToLower();
                query = query.Where(a =>
                    a.ReceiptId.ToLower().Contains(lower) ||
                    a.Items.Any(i => i.ProductName.ToLower().Contains(lower))
                );
            }

            foreach (var log in query) AuditLogs.Add(log);
        }
    }

    public class AuditLogItem
    {
        public string ReceiptId { get; set; } = "";
        public DateTime Date { get; set; }
        public string ActionType { get; set; } = ""; // "RETURN" or "VOID"
        public decimal TotalItems { get; set; }
        public decimal TotalValue { get; set; }
        public List<AuditDetailItem> Items { get; set; } = new();

        public string ActionColor => ActionType == "VOID" ? "#EF4444" : "#F59E0B"; // Red for Void, Orange for Return
        public string ActionBgColor => ActionType == "VOID" ? "#FEE2E2" : "#FEF3C7";
    }

    public class AuditDetailItem
    {
        public string ProductName { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total => Quantity * UnitPrice;
        public string Reason { get; set; } = "";
    }
}