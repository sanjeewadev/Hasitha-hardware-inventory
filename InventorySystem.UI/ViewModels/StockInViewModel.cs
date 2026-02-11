using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class StockInViewModel : ViewModelBase
    {
        private readonly Data.Context.InventoryDbContext _context;

        // --- PAGE VISIBILITY LOGIC ---
        private bool _isPage1Visible = true;
        public bool IsPage1Visible { get => _isPage1Visible; set { _isPage1Visible = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPage2Visible)); } }
        public bool IsPage2Visible => !IsPage1Visible;

        // ==========================================
        // PAGE 1: DRAFTS & NEW BILL ENTRY
        // ==========================================
        public ObservableCollection<Supplier> Suppliers { get; } = new();
        public ObservableCollection<PurchaseInvoice> DraftInvoices { get; } = new();

        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier { get => _selectedSupplier; set { _selectedSupplier = value; OnPropertyChanged(); } }

        private string _billNumber = "";
        public string BillNumber { get => _billNumber; set { _billNumber = value; OnPropertyChanged(); } }

        private DateTime _billDate = DateTime.Now;
        public DateTime BillDate { get => _billDate; set { _billDate = value; OnPropertyChanged(); } }

        public ICommand CreateDraftCommand { get; }
        public ICommand ResumeDraftCommand { get; }
        public ICommand DeleteDraftCommand { get; } // NEW

        // ==========================================
        // PAGE 2: WORKSPACE (ITEM ENTRY)
        // ==========================================
        public PurchaseInvoice? CurrentInvoice { get; private set; }
        public ObservableCollection<StockBatch> CurrentBatches { get; } = new();

        public ObservableCollection<Category> DisplayCategories { get; } = new();
        public ObservableCollection<Product> DisplayProducts { get; } = new();

        private Category? _currentParentCategory;

        private string _productSearchText = "";
        public string ProductSearchText
        {
            get => _productSearchText;
            set { _productSearchText = value; OnPropertyChanged(); SearchProducts(); }
        }

        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                _selectedProduct = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedProductUnit));
                if (value != null)
                {
                    BuyingPrice = value.BuyingPrice;
                    SellingPrice = value.SellingPrice;
                    MaxDiscount = value.DiscountLimit;
                }
            }
        }

        public string SelectedProductUnit => SelectedProduct?.Unit ?? "Units";

        private decimal _quantity;
        public decimal Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(); } }

        private decimal _buyingPrice;
        public decimal BuyingPrice { get => _buyingPrice; set { _buyingPrice = value; OnPropertyChanged(); } }

        private decimal _sellingPrice;
        public decimal SellingPrice { get => _sellingPrice; set { _sellingPrice = value; OnPropertyChanged(); } }

        private decimal _maxDiscount;
        public decimal MaxDiscount { get => _maxDiscount; set { _maxDiscount = value; OnPropertyChanged(); } }

        public decimal TotalBillAmount => CurrentBatches.Sum(b => b.TotalLineCost);

        public ICommand AddItemCommand { get; }
        public ICommand RemoveItemCommand { get; } // NEW
        public ICommand PostInvoiceCommand { get; }
        public ICommand BackToPage1Command { get; }
        public ICommand SelectCategoryCommand { get; }
        public ICommand GoUpCategoryCommand { get; }

        public StockInViewModel()
        {
            _context = DatabaseService.CreateDbContext();

            CreateDraftCommand = new RelayCommand(async () => await CreateNewDraftAsync());
            ResumeDraftCommand = new RelayCommand<PurchaseInvoice>(async (inv) => await LoadDraftAsync(inv));
            DeleteDraftCommand = new RelayCommand<PurchaseInvoice>(async (inv) => await DeleteDraftAsync(inv));

            AddItemCommand = new RelayCommand(async () => await AddItemToBillAsync());
            RemoveItemCommand = new RelayCommand<StockBatch>(async (batch) => await RemoveItemAsync(batch));
            PostInvoiceCommand = new RelayCommand(async () => await PostInvoiceAsync());
            BackToPage1Command = new RelayCommand(() => { IsPage1Visible = true; LoadPage1Data(); });

            SelectCategoryCommand = new RelayCommand<Category>(SelectCategory);
            GoUpCategoryCommand = new RelayCommand(LoadTopLevelCategories);

            LoadPage1Data();
        }

        private async void LoadPage1Data()
        {
            var suppliers = await _context.Suppliers.OrderBy(s => s.Name).ToListAsync();
            Suppliers.Clear();
            foreach (var s in suppliers) Suppliers.Add(s);

            var drafts = await _context.PurchaseInvoices
                .Include(i => i.Supplier)
                .Include(i => i.Batches)
                .Where(i => i.Status == InvoiceStatus.Draft)
                .OrderByDescending(i => i.Date)
                .ToListAsync();

            DraftInvoices.Clear();
            foreach (var d in drafts) DraftInvoices.Add(d);
        }

        private async Task CreateNewDraftAsync()
        {
            if (SelectedSupplier == null || string.IsNullOrWhiteSpace(BillNumber))
            {
                MessageBox.Show("Please select a supplier and enter a bill number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool exists = await _context.PurchaseInvoices.AnyAsync(i => i.BillNumber == BillNumber && i.SupplierId == SelectedSupplier.Id);
            if (exists)
            {
                MessageBox.Show("This Bill Number already exists for this supplier!", "Duplicate Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var newInvoice = new PurchaseInvoice
            {
                SupplierId = SelectedSupplier.Id,
                BillNumber = BillNumber,
                Date = BillDate,
                Status = InvoiceStatus.Draft,
                Note = "Created via GRN"
            };

            _context.PurchaseInvoices.Add(newInvoice);
            await _context.SaveChangesAsync();

            await LoadDraftAsync(newInvoice);
            SelectedSupplier = null;
            BillNumber = "";
        }

        private async Task LoadDraftAsync(PurchaseInvoice invoice)
        {
            if (invoice == null) return;

            CurrentInvoice = await _context.PurchaseInvoices
                .Include(i => i.Supplier)
                .Include(i => i.Batches)
                .ThenInclude(b => b.Product)
                .FirstOrDefaultAsync(i => i.Id == invoice.Id);

            CurrentBatches.Clear();
            if (CurrentInvoice?.Batches != null)
            {
                foreach (var batch in CurrentInvoice.Batches) CurrentBatches.Add(batch);
            }

            OnPropertyChanged(nameof(TotalBillAmount));
            LoadTopLevelCategories();
            SearchProducts();

            IsPage1Visible = false;
        }

        // --- NEW: Delete Draft Logic ---
        private async Task DeleteDraftAsync(PurchaseInvoice invoice)
        {
            if (invoice == null) return;
            if (MessageBox.Show($"Delete the draft bill '{invoice.BillNumber}'?\nThis cannot be undone.", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _context.PurchaseInvoices.Remove(invoice);
                await _context.SaveChangesAsync();
                LoadPage1Data();
            }
        }

        private async void LoadTopLevelCategories()
        {
            _currentParentCategory = null;
            var topCats = await _context.Categories.Where(c => c.ParentId == null).OrderBy(c => c.Name).ToListAsync();

            DisplayCategories.Clear();
            foreach (var c in topCats) DisplayCategories.Add(c);

            SearchProducts();
        }

        private async void SelectCategory(Category cat)
        {
            if (cat == null) return;
            _currentParentCategory = cat;

            var subCats = await _context.Categories.Where(c => c.ParentId == cat.Id).OrderBy(c => c.Name).ToListAsync();
            DisplayCategories.Clear();
            foreach (var c in subCats) DisplayCategories.Add(c);

            SearchProducts();
        }

        private async void SearchProducts()
        {
            var query = _context.Products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(ProductSearchText))
            {
                var lower = ProductSearchText.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(lower) || p.Barcode.Contains(lower));
            }

            if (_currentParentCategory != null)
            {
                query = query.Where(p => p.CategoryId == _currentParentCategory.Id);
            }

            var results = await query.Take(20).ToListAsync();

            DisplayProducts.Clear();
            foreach (var p in results) DisplayProducts.Add(p);
        }

        private async Task AddItemToBillAsync()
        {
            if (CurrentInvoice == null) return;
            if (SelectedProduct == null) { MessageBox.Show("Select a product first."); return; }

            // Validation
            if (Quantity <= 0) { MessageBox.Show("Quantity must be greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (BuyingPrice < 0) { MessageBox.Show("Buying Cost cannot be negative.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (SellingPrice < BuyingPrice)
            {
                if (MessageBox.Show("Warning: Selling Price is lower than Buying Cost. Do you want to proceed?", "Pricing Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No) return;
            }

            // Duplicate Check
            var existingBatch = CurrentBatches.FirstOrDefault(b => b.ProductId == SelectedProduct.Id);
            if (existingBatch != null)
            {
                if (MessageBox.Show($"'{SelectedProduct.Name}' is already in this bill.\nDo you want to add to its quantity and update the prices?", "Duplicate Found", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    existingBatch.InitialQuantity += Quantity;
                    existingBatch.RemainingQuantity += Quantity;
                    existingBatch.CostPrice = BuyingPrice;
                    existingBatch.SellingPrice = SellingPrice;
                    existingBatch.Discount = MaxDiscount;
                    _context.StockBatches.Update(existingBatch);
                    await _context.SaveChangesAsync();
                    await LoadDraftAsync(CurrentInvoice);
                    ClearInputs();
                }
                return;
            }

            var batch = new StockBatch
            {
                PurchaseInvoiceId = CurrentInvoice.Id,
                ProductId = SelectedProduct.Id,
                InitialQuantity = Quantity,
                RemainingQuantity = Quantity,
                CostPrice = BuyingPrice,
                SellingPrice = SellingPrice,
                Discount = MaxDiscount,
                ReceivedDate = CurrentInvoice.Date
            };

            _context.StockBatches.Add(batch);
            await _context.SaveChangesAsync();
            await LoadDraftAsync(CurrentInvoice);

            ClearInputs();
        }

        // --- NEW: Remove Item Logic ---
        private async Task RemoveItemAsync(StockBatch batch)
        {
            if (batch == null || CurrentInvoice == null) return;

            if (MessageBox.Show($"Remove '{batch.Product?.Name}' from this GRN?", "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _context.StockBatches.Remove(batch);
                await _context.SaveChangesAsync();
                await LoadDraftAsync(CurrentInvoice); // This auto-refreshes the grid and the Grand Total math
            }
        }

        private void ClearInputs()
        {
            Quantity = 0;
            ProductSearchText = "";
            SelectedProduct = null;
        }

        private async Task PostInvoiceAsync()
        {
            if (CurrentInvoice == null || CurrentBatches.Count == 0)
            {
                MessageBox.Show("Cannot post an empty invoice.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to POST this bill for Rs {TotalBillAmount:N2}?\nThis will lock the bill and update live inventory. You cannot edit prices after posting.", "Confirm Post", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                CurrentInvoice.Status = InvoiceStatus.Posted;
                CurrentInvoice.TotalAmount = TotalBillAmount;
                _context.PurchaseInvoices.Update(CurrentInvoice);

                foreach (var batch in CurrentBatches)
                {
                    var product = await _context.Products.FindAsync(batch.ProductId);
                    if (product != null)
                    {
                        product.Quantity += batch.InitialQuantity;
                        product.BuyingPrice = batch.CostPrice;
                        product.SellingPrice = batch.SellingPrice;
                        product.DiscountLimit = batch.Discount;
                        _context.Products.Update(product);

                        var move = new StockMovement
                        {
                            ProductId = batch.ProductId,
                            StockBatchId = batch.Id,
                            Type = StockMovementType.In,
                            Quantity = batch.InitialQuantity,
                            UnitCost = batch.CostPrice,
                            UnitPrice = batch.SellingPrice,
                            Date = CurrentInvoice.Date,
                            Note = $"GRN: {CurrentInvoice.BillNumber}"
                        };
                        _context.StockMovements.Add(move);
                    }
                }

                await _context.SaveChangesAsync();
                MessageBox.Show("Bill successfully POSTED! Stock is now live.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                IsPage1Visible = true;
                LoadPage1Data();
            }
        }
    }
}