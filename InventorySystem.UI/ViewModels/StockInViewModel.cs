using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services; // Assuming DatabaseService is here
using InventorySystem.UI.Commands;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class StockInViewModel : ViewModelBase
    {
        private readonly InventorySystem.Data.Context.InventoryDbContext _context;

        // --- COLLECTIONS ---
        public ObservableCollection<Supplier> Suppliers { get; } = new();
        public ObservableCollection<Product> ProductSearchResults { get; } = new();

        // This is the "Draft Bill" - Items waiting to be saved
        public ObservableCollection<StockBatch> DraftBatches { get; } = new();

        // --- HEADER INPUTS (The Bill) ---
        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier { get => _selectedSupplier; set { _selectedSupplier = value; OnPropertyChanged(); } }

        private string _billNumber = "";
        public string BillNumber { get => _billNumber; set { _billNumber = value; OnPropertyChanged(); } }

        private DateTime _billDate = DateTime.Now;
        public DateTime BillDate { get => _billDate; set { _billDate = value; OnPropertyChanged(); } }

        // --- ITEM INPUTS (The Workspace) ---
        private string _productSearchText = "";
        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                _productSearchText = value;
                OnPropertyChanged();
                SearchProducts();
            }
        }

        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                _selectedProduct = value;
                OnPropertyChanged();
                if (value != null)
                {
                    // Auto-fill cost/price from product history if possible
                    CostPrice = value.BuyingPrice; // Assuming you have this
                    SellingPrice = value.SellingPrice;
                }
            }
        }

        private decimal _quantity;
        public decimal Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(); } }

        private decimal _costPrice;
        public decimal CostPrice { get => _costPrice; set { _costPrice = value; OnPropertyChanged(); } }

        private decimal _sellingPrice;
        public decimal SellingPrice { get => _sellingPrice; set { _sellingPrice = value; OnPropertyChanged(); } }

        // --- TOTALS ---
        public decimal TotalBillAmount => DraftBatches.Sum(b => b.CostPrice * b.InitialQuantity);

        // --- COMMANDS ---
        public ICommand AddToDraftCommand { get; }
        public ICommand RemoveFromDraftCommand { get; }
        public ICommand SaveInvoiceCommand { get; }
        public ICommand ClearAllCommand { get; }

        public StockInViewModel()
        {
            _context = DatabaseService.CreateDbContext();

            AddToDraftCommand = new RelayCommand(AddToDraft);
            RemoveFromDraftCommand = new RelayCommand<StockBatch>(RemoveFromDraft);
            SaveInvoiceCommand = new RelayCommand(async () => await SaveInvoiceAsync());
            ClearAllCommand = new RelayCommand(ClearAll);

            LoadSuppliers();
        }

        private async void LoadSuppliers()
        {
            var list = await _context.Suppliers.OrderBy(s => s.Name).ToListAsync();
            Suppliers.Clear();
            foreach (var s in list) Suppliers.Add(s);
        }

        private async void SearchProducts()
        {
            if (string.IsNullOrWhiteSpace(ProductSearchText))
            {
                ProductSearchResults.Clear();
                return;
            }

            var lower = ProductSearchText.ToLower();
            var results = await _context.Products
                .Where(p => p.Name.ToLower().Contains(lower) || p.Barcode.Contains(lower))
                .Take(10) // Limit results for speed
                .ToListAsync();

            ProductSearchResults.Clear();
            foreach (var p in results) ProductSearchResults.Add(p);
        }

        private void AddToDraft()
        {
            if (SelectedProduct == null) { MessageBox.Show("Select a product first."); return; }
            if (Quantity <= 0) { MessageBox.Show("Quantity must be > 0."); return; }

            // Create a Temporary Batch (In Memory)
            var batch = new StockBatch
            {
                ProductId = SelectedProduct.Id,
                Product = SelectedProduct, // Link for display purposes
                InitialQuantity = Quantity,
                RemainingQuantity = Quantity,
                CostPrice = CostPrice,
                SellingPrice = SellingPrice,
                ReceivedDate = BillDate
            };

            DraftBatches.Add(batch);
            OnPropertyChanged(nameof(TotalBillAmount));

            // Reset Inputs for next item
            SelectedProduct = null;
            ProductSearchText = "";
            Quantity = 0;
            // Keep Price/Cost as they might be similar for next item
        }

        private void RemoveFromDraft(StockBatch batch)
        {
            if (batch != null)
            {
                DraftBatches.Remove(batch);
                OnPropertyChanged(nameof(TotalBillAmount));
            }
        }

        private async Task SaveInvoiceAsync()
        {
            // 1. Validation
            if (SelectedSupplier == null) { MessageBox.Show("Please select a Supplier."); return; }
            if (string.IsNullOrWhiteSpace(BillNumber)) { MessageBox.Show("Please enter a Bill Number."); return; }
            if (DraftBatches.Count == 0) { MessageBox.Show("No items in the draft list."); return; }

            // 2. Check for Duplicate Bill Number
            bool exists = await _context.PurchaseInvoices.AnyAsync(i => i.BillNumber == BillNumber && i.SupplierId == SelectedSupplier.Id);
            if (exists)
            {
                MessageBox.Show($"Bill #{BillNumber} already exists for this supplier!", "Duplicate Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // 3. Create the Parent Invoice
                var invoice = new PurchaseInvoice
                {
                    SupplierId = SelectedSupplier.Id,
                    BillNumber = BillNumber,
                    Date = BillDate,
                    TotalAmount = TotalBillAmount,
                    Note = "Stock In via GRN"
                };

                _context.PurchaseInvoices.Add(invoice);
                await _context.SaveChangesAsync(); // Save to get the Invoice ID

                // 4. Link Batches to Invoice and Save them
                foreach (var batch in DraftBatches)
                {
                    batch.PurchaseInvoiceId = invoice.Id;
                    batch.Product = null; // Prevent EF from trying to re-create the product

                    _context.StockBatches.Add(batch);

                    // 4.1 Update Product Global Quantity
                    var product = await _context.Products.FindAsync(batch.ProductId);
                    if (product != null)
                    {
                        product.Quantity += batch.InitialQuantity;
                        product.BuyingPrice = batch.CostPrice; // Update last buying price
                        product.SellingPrice = batch.SellingPrice; // Update selling price
                    }

                    // 4.2 Log Movement
                    var move = new StockMovement
                    {
                        ProductId = batch.ProductId,
                        Type = Core.Enums.StockMovementType.In,
                        Quantity = batch.InitialQuantity,
                        UnitCost = batch.CostPrice,
                        UnitPrice = batch.SellingPrice,
                        Date = BillDate,
                        Note = $"GRN: {BillNumber}"
                    };
                    _context.StockMovements.Add(move);
                }

                await _context.SaveChangesAsync();

                MessageBox.Show("Stock In Saved Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving invoice: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearAll()
        {
            DraftBatches.Clear();
            SelectedSupplier = null;
            BillNumber = "";
            BillDate = DateTime.Now;
            ProductSearchText = "";
            SelectedProduct = null;
            OnPropertyChanged(nameof(TotalBillAmount));
        }
    }
}