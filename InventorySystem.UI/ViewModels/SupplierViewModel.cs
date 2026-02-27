using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
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
    public class SupplierViewModel : ViewModelBase
    {
        private readonly ISupplierRepository _supplierRepo;
        private readonly Data.Context.InventoryDbContext _context;

        // --- PAGE VISIBILITY ---
        private bool _isPage1Visible = true;
        public bool IsPage1Visible
        {
            get => _isPage1Visible;
            set { _isPage1Visible = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPage2Visible)); }
        }
        public bool IsPage2Visible => !IsPage1Visible;

        // --- PAGE 1: LIST ---
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
                else if (value == null)
                {
                    IsEditMode = false;
                }
            }
        }

        private bool _isEditMode;
        public bool IsEditMode { get => _isEditMode; set { _isEditMode = value; OnPropertyChanged(); } }

        public ICommand SaveCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ViewDetailsCommand { get; }

        // --- PAGE 2: HISTORY ---
        public ObservableCollection<PurchaseInvoice> SupplierInvoices { get; } = new();

        private Supplier? _supplierForDetails;
        public Supplier? SupplierForDetails { get => _supplierForDetails; private set { _supplierForDetails = value; OnPropertyChanged(); } }

        private List<PurchaseInvoice> _masterInvoiceList = new();

        private string _searchInvoiceText = "";
        public string SearchInvoiceText
        {
            get => _searchInvoiceText;
            set { _searchInvoiceText = value; OnPropertyChanged(); FilterInvoices(); }
        }

        public ICommand BackToPage1Command { get; }

        public SupplierViewModel()
        {
            _context = DatabaseService.CreateDbContext();
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
                list = list.Where(s => s.Name.ToLower().Contains(lower) || (s.Phone != null && s.Phone.Contains(lower))).ToList();
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

            try
            {
                // FIX: Proactive Duplicate Check
                var allSuppliers = await _supplierRepo.GetAllAsync();
                var duplicate = allSuppliers.FirstOrDefault(s => s.Name.Equals(Name.Trim(), StringComparison.OrdinalIgnoreCase));

                if (IsEditMode && SelectedSupplier != null)
                {
                    // If editing, make sure the name isn't taken by a DIFFERENT supplier
                    if (duplicate != null && duplicate.Id != SelectedSupplier.Id)
                    {
                        MessageBox.Show($"A supplier named '{Name.Trim()}' already exists.", "Duplicate Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    SelectedSupplier.Name = Name.Trim();
                    SelectedSupplier.Phone = Phone?.Trim() ?? "";
                    SelectedSupplier.Note = Note?.Trim() ?? "";
                    await _supplierRepo.UpdateAsync(SelectedSupplier);
                }
                else
                {
                    // If adding new, ANY duplicate is a block
                    if (duplicate != null)
                    {
                        MessageBox.Show($"A supplier named '{Name.Trim()}' already exists.", "Duplicate Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var newSupplier = new Supplier { Name = Name.Trim(), Phone = Phone?.Trim() ?? "", Note = Note?.Trim() ?? "" };
                    await _supplierRepo.AddAsync(newSupplier);
                }

                ClearForm();
                LoadData();
            }
            catch (Exception ex)
            {
                // Safety net: Catch any other DB errors so the app never crashes
                MessageBox.Show($"An error occurred while saving: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteSupplier(Supplier s)
        {
            if (s == null) return;

            // Strict Check: Are there ANY invoices linked?
            var hasInvoices = await _context.PurchaseInvoices.AnyAsync(i => i.SupplierId == s.Id);
            if (hasInvoices)
            {
                MessageBox.Show($"Cannot delete '{s.Name}'.\n\nThis supplier has linked purchase history. Deleting them would corrupt financial records.", "Action Blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (MessageBox.Show($"Delete supplier '{s.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    await _supplierRepo.DeleteAsync(s.Id);
                    LoadData();
                    ClearForm();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
            SearchInvoiceText = "";

            var invoices = await _context.PurchaseInvoices
                .Include(i => i.Batches)
                    .ThenInclude(b => b.Product)
                .Where(i => i.SupplierId == s.Id)
                .OrderByDescending(i => i.Date)
                .ToListAsync();

            _masterInvoiceList = invoices;
            FilterInvoices();

            IsPage1Visible = false;
        }

        private void FilterInvoices()
        {
            SupplierInvoices.Clear();
            var query = _masterInvoiceList.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchInvoiceText))
            {
                var lower = SearchInvoiceText.ToLower();
                query = query.Where(i =>
                    i.BillNumber.ToLower().Contains(lower) ||
                    i.Batches.Any(b => b.Product.Name.ToLower().Contains(lower))
                );
            }

            foreach (var inv in query) SupplierInvoices.Add(inv);
        }
    }
}