using InventorySystem.Core.Entities;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class CreditManagerViewModel : ViewModelBase
    {
        private readonly CreditService _creditService;

        // --- MAIN DATA ---
        private List<SalesTransaction> _allTransactionsCache = new();
        public ObservableCollection<SalesTransaction> UnpaidTransactions { get; } = new();

        // --- SEARCH ---
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterTransactions(); }
        }

        private SalesTransaction? _selectedTransaction;
        public SalesTransaction? SelectedTransaction
        {
            get => _selectedTransaction;
            set { _selectedTransaction = value; OnPropertyChanged(); }
        }

        // --- POPUP STATES ---
        private bool _isPaymentPopupVisible;
        public bool IsPaymentPopupVisible { get => _isPaymentPopupVisible; set { _isPaymentPopupVisible = value; OnPropertyChanged(); } }

        private bool _isDetailsPopupVisible;
        public bool IsDetailsPopupVisible { get => _isDetailsPopupVisible; set { _isDetailsPopupVisible = value; OnPropertyChanged(); } }

        private decimal _paymentAmountInput;
        public decimal PaymentAmountInput { get => _paymentAmountInput; set { _paymentAmountInput = value; OnPropertyChanged(); } }

        private string _paymentNote = "";
        public string PaymentNote { get => _paymentNote; set { _paymentNote = value; OnPropertyChanged(); } }

        private bool _isFullPayment;
        public bool IsFullPayment
        {
            get => _isFullPayment;
            set
            {
                _isFullPayment = value;
                OnPropertyChanged();
                if (value && SelectedTransaction != null)
                {
                    PaymentAmountInput = SelectedTransaction.RemainingBalance;
                }
            }
        }

        public ObservableCollection<StockMovement> SelectedSaleItems { get; } = new();

        // --- COMMANDS ---
        public ICommand LoadDataCommand { get; }
        public ICommand OpenPaymentPopupCommand { get; }
        public ICommand ClosePaymentPopupCommand { get; }
        public ICommand SubmitPaymentCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand CloseDetailsCommand { get; }
        public ICommand PrintReceiptCommand { get; } // <--- NEW

        public CreditManagerViewModel(CreditService creditService)
        {
            _creditService = creditService;

            LoadDataCommand = new RelayCommand(async () => await LoadCredits());

            // Payment Logic
            OpenPaymentPopupCommand = new RelayCommand<SalesTransaction>(tx =>
            {
                SelectedTransaction = tx;
                PaymentAmountInput = 0;
                IsFullPayment = false;
                PaymentNote = "";
                IsPaymentPopupVisible = true;
            });

            ClosePaymentPopupCommand = new RelayCommand(() => IsPaymentPopupVisible = false);
            SubmitPaymentCommand = new RelayCommand(async () => await ExecutePayment());

            // Details Logic
            ViewDetailsCommand = new RelayCommand<SalesTransaction>(async (tx) => await LoadAndShowDetails(tx));
            CloseDetailsCommand = new RelayCommand(() => IsDetailsPopupVisible = false);

            // Print Logic (NEW)
            PrintReceiptCommand = new RelayCommand(PrintCurrentSelection);

            _ = LoadCredits();
        }

        private async Task LoadCredits()
        {
            var list = await _creditService.GetUnpaidTransactionsAsync();
            _allTransactionsCache = list;
            FilterTransactions();
        }

        private void FilterTransactions()
        {
            UnpaidTransactions.Clear();
            var query = _allTransactionsCache.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lowerSearch = SearchText.ToLower();
                query = query.Where(t =>
                    (t.CustomerName != null && t.CustomerName.ToLower().Contains(lowerSearch)) ||
                    t.ReceiptId.Contains(lowerSearch)
                );
            }

            foreach (var item in query) UnpaidTransactions.Add(item);
        }

        private async Task LoadAndShowDetails(SalesTransaction tx)
        {
            if (tx == null) return;
            SelectedTransaction = tx;
            var items = await _creditService.GetTransactionItemsAsync(tx.ReceiptId);
            SelectedSaleItems.Clear();
            foreach (var item in items) SelectedSaleItems.Add(item);
            IsDetailsPopupVisible = true;
        }

        private async Task ExecutePayment()
        {
            if (SelectedTransaction == null) return;
            if (PaymentAmountInput <= 0) return;

            try
            {
                await _creditService.AddPaymentAsync(SelectedTransaction.ReceiptId, PaymentAmountInput, PaymentNote);
                IsPaymentPopupVisible = false;
                await LoadCredits();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Payment Failed: {ex.Message}");
            }
        }

        private void PrintCurrentSelection()
        {
            if (SelectedTransaction == null) return;

            try
            {
                // 1. Get Settings from UI
                string printerName = Properties.Settings.Default.PrinterName;
                int copies = Properties.Settings.Default.ReceiptCopies;

                var printService = new PrintService();

                string txt = $"RECEIPT RE-PRINT\nID: {SelectedTransaction.ReceiptId}\nCustomer: {SelectedTransaction.CustomerName}\n";
                txt += $"----------------\n";
                foreach (var item in SelectedSaleItems)
                {
                    txt += $"{item.Product?.Name} x{item.Quantity}  {item.LineTotal}\n";
                }
                txt += $"----------------\nTotal: {SelectedTransaction.TotalAmount}\nPaid: {SelectedTransaction.PaidAmount}\nDue: {SelectedTransaction.RemainingBalance}";

                // 2. Pass Settings to Service
                printService.PrintReceipt(SelectedTransaction.ReceiptId, txt, printerName, copies);

                System.Windows.MessageBox.Show("Sent to printer.");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Print Error: {ex.Message}");
            }
        }
    }
}