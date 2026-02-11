using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class SupplierViewModel : ViewModelBase
    {
        private readonly ISupplierRepository _supplierRepo;
        private readonly Data.Context.InventoryDbContext _context;

        // --- PAGE VISIBILITY LOGIC ---
        private bool _isPage1Visible = true;
        public bool IsPage1Visible { get => _isPage1Visible; set { _isPage1Visible = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPage2Visible)); } }
        public bool IsPage2Visible => !IsPage1Visible;

        // ==========================================
        // PAGE 1: SUPPLIER MANAGEMENT
        // ==========================================
        public ObservableCollection<Supplier> Suppliers { get; } = new();

        private string _name = "";
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private string _phone = "";
        public string Phone { get => _phone; set { _phone = value; OnPropertyChanged(); } }

        private string _note = "";
        public string Note { get => _note; set { _note = value; OnPropertyChanged(); } }

        private string _searchText = "";
        public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); LoadData(); } }

        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                _selectedSupplier = value;
                OnPropertyChanged();
                if (value != null && IsPage1Visible)
                {
                    Name = value.Name;
                    Phone = value.Phone;
                    Note = value.Note;
                    IsEditMode = true;
                }
            }
        }

        private bool _isEditMode;
        public bool IsEditMode { get => _isEditMode; set { _isEditMode = value; OnPropertyChanged(); } }

        public ICommand SaveCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ViewDetailsCommand { get; }

        // ==========================================
        // PAGE 2: SUPPLIER BILLS & PRODUCTS
        // ==========================================
        public ObservableCollection<PurchaseInvoice> SupplierInvoices { get; } = new();
        public Supplier? SupplierForDetails { get; private set; }

        private List<PurchaseInvoice> _masterInvoiceList = new(); // Stores all bills for instant filtering

        private string _searchInvoiceText = "";
        public string SearchInvoiceText
        {
            get => _searchInvoiceText;
            set { _searchInvoiceText = value; OnPropertyChanged(); FilterInvoices(); }
        }

        public ICommand BackToPage1Command { get; }

        public SupplierViewModel()
        {
            _context = InventorySystem.Infrastructure.Services.DatabaseService.CreateDbContext();
            _supplierRepo = new SupplierRepository(_context);

            SaveCommand = new RelayCommand(async () => await SaveSupplier());
            ClearCommand = new RelayCommand(ClearForm);
            DeleteCommand = new RelayCommand<Supplier>(async (s) => await DeleteSupplier(s));

            ViewDetailsCommand = new RelayCommand<Supplier>(async (s) => await LoadSupplierDetails(s));
            BackToPage1Command = new RelayCommand(() => { IsPage1Visible = true; LoadData(); });

            LoadData();
        }

        private async void LoadData()
        {
            var list = await _supplierRepo.GetAllAsync();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lower = SearchText.ToLower();
                list = list.Where(s => s.Name.ToLower().Contains(lower) || s.Phone.Contains(lower)).ToList();
            }

            Suppliers.Clear();
            foreach (var s in list) Suppliers.Add(s);
        }

        private async Task SaveSupplier()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                MessageBox.Show("Supplier Name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsEditMode && SelectedSupplier != null)
            {
                SelectedSupplier.Name = Name;
                SelectedSupplier.Phone = Phone;
                SelectedSupplier.Note = Note;
                await _supplierRepo.UpdateAsync(SelectedSupplier);
            }
            else
            {
                var newSupplier = new Supplier { Name = Name, Phone = Phone, Note = Note };
                await _supplierRepo.AddAsync(newSupplier);
            }

            ClearForm();
            LoadData();
        }

        private async Task DeleteSupplier(Supplier s)
        {
            if (s == null) return;
            if (MessageBox.Show($"Delete supplier '{s.Name}'?\n(Only possible if they have no linked bills)", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                await _supplierRepo.DeleteAsync(s.Id);
                LoadData();
            }
        }

        private void ClearForm()
        {
            Name = ""; Phone = ""; Note = "";
            SelectedSupplier = null;
            IsEditMode = false;
        }

        private async Task LoadSupplierDetails(Supplier s)
        {
            if (s == null) return;
            SupplierForDetails = s;
            SearchInvoiceText = ""; // Clear old search
            OnPropertyChanged(nameof(SupplierForDetails));

            // Fetch deeply
            var invoices = await _context.PurchaseInvoices
                .Include(i => i.Batches)
                    .ThenInclude(b => b.Product)
                .Where(i => i.SupplierId == s.Id)
                .OrderByDescending(i => i.Date)
                .ToListAsync();

            _masterInvoiceList = invoices; // Save to master list
            FilterInvoices(); // Trigger initial load

            IsPage1Visible = false;
        }

        private void FilterInvoices()
        {
            SupplierInvoices.Clear();

            var query = _masterInvoiceList.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchInvoiceText))
            {
                var lower = SearchInvoiceText.ToLower();
                // Search by Bill Number OR if any Product Name inside the bill matches
                query = query.Where(i => i.BillNumber.ToLower().Contains(lower) ||
                                         i.Batches.Any(b => b.Product.Name.ToLower().Contains(lower)));
            }

            foreach (var inv in query)
            {
                SupplierInvoices.Add(inv);
            }
        }
    }
}