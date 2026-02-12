using InventorySystem.Core.Entities;
using InventorySystem.Infrastructure.Services;
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
        public ICommand PrintReceiptCommand { get; }

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
                IsPaymentPopupVisible = true;
            });

            ClosePaymentPopupCommand = new RelayCommand(() => IsPaymentPopupVisible = false);
            SubmitPaymentCommand = new RelayCommand(async () => await ExecutePayment());

            // Details Logic
            ViewDetailsCommand = new RelayCommand<SalesTransaction>(async (tx) => await LoadAndShowDetails(tx));
            CloseDetailsCommand = new RelayCommand(() => IsDetailsPopupVisible = false);

            // Print Logic
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
            if (PaymentAmountInput <= 0)
            {
                MessageBox.Show("Please enter a valid amount.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (PaymentAmountInput > SelectedTransaction.RemainingBalance)
            {
                MessageBox.Show($"Amount cannot exceed the balance of Rs {SelectedTransaction.RemainingBalance:N2}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Note removed as requested
                await _creditService.AddPaymentAsync(SelectedTransaction.ReceiptId, PaymentAmountInput, "Payment Received");
                IsPaymentPopupVisible = false;
                MessageBox.Show("Payment Recorded Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadCredits();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Payment Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintCurrentSelection()
        {
            if (SelectedTransaction == null) return;

            try
            {
                string printerName = "";
                int copies = 1;
                try
                {
                    printerName = Properties.Settings.Default.PrinterName;
                    copies = Properties.Settings.Default.ReceiptCopies;
                }
                catch { }

                var printService = new PrintService();

                string txt = $"CREDIT INVOICE (COPY)\nID: {SelectedTransaction.ReceiptId}\nCustomer: {SelectedTransaction.CustomerName}\n";
                txt += $"Date: {SelectedTransaction.TransactionDate}\n----------------\n";
                foreach (var item in SelectedSaleItems)
                {
                    txt += $"{item.Product?.Name} x{item.Quantity}  {item.LineTotal:N2}\n";
                }
                txt += $"----------------\nTotal Bill: {SelectedTransaction.TotalAmount:N2}\nPaid So Far: {SelectedTransaction.PaidAmount:N2}\nBalance Due: {SelectedTransaction.RemainingBalance:N2}";

                printService.PrintReceipt(SelectedTransaction.ReceiptId, txt, printerName, copies);

                MessageBox.Show("Sent to printer.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print Error: {ex.Message}");
            }
        }
    }
}