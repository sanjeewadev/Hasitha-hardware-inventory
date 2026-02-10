using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
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

        public ObservableCollection<Supplier> Suppliers { get; } = new();

        // --- FORM INPUTS ---
        private string _name = "";
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private string _phone = "";
        public string Phone { get => _phone; set { _phone = value; OnPropertyChanged(); } }

        private string _email = "";
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }

        private string _address = "";
        public string Address { get => _address; set { _address = value; OnPropertyChanged(); } }

        private string _note = "";
        public string Note { get => _note; set { _note = value; OnPropertyChanged(); } }

        // --- SEARCH ---
        private string _searchText = "";
        public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); LoadData(); } }

        // --- SELECTION STATE ---
        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                _selectedSupplier = value;
                OnPropertyChanged();
                if (value != null)
                {
                    // Populate Form for Editing
                    Name = value.Name;
                    Phone = value.Phone;
                    Email = value.Email;
                    Address = value.Address;
                    Note = value.Note;
                    IsEditMode = true;
                }
            }
        }

        private bool _isEditMode;
        public bool IsEditMode { get => _isEditMode; set { _isEditMode = value; OnPropertyChanged(); } }

        // --- COMMANDS ---
        public ICommand SaveCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand DeleteCommand { get; }

        public SupplierViewModel()
        {
            // Ideally, pass this via Constructor Injection if you have it set up.
            // For now, we create it manually to keep it simple.
            var db = InventorySystem.Infrastructure.Services.DatabaseService.CreateDbContext();
            _supplierRepo = new SupplierRepository(db);

            SaveCommand = new RelayCommand(async () => await SaveSupplier());
            ClearCommand = new RelayCommand(ClearForm);
            DeleteCommand = new RelayCommand<Supplier>(async (s) => await DeleteSupplier(s));

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
                // Update Existing
                SelectedSupplier.Name = Name;
                SelectedSupplier.Phone = Phone;
                SelectedSupplier.Email = Email;
                SelectedSupplier.Address = Address;
                SelectedSupplier.Note = Note;

                await _supplierRepo.UpdateAsync(SelectedSupplier);
                MessageBox.Show("Supplier updated successfully!", "Saved");
            }
            else
            {
                // Create New
                var newSupplier = new Supplier
                {
                    Name = Name,
                    Phone = Phone,
                    Email = Email,
                    Address = Address,
                    Note = Note
                };

                await _supplierRepo.AddAsync(newSupplier);
                MessageBox.Show("New Supplier added!", "Saved");
            }

            ClearForm();
            LoadData();
        }

        private async Task DeleteSupplier(Supplier s)
        {
            if (MessageBox.Show($"Delete supplier '{s.Name}'?\n(Only possible if they have no linked bills)", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                await _supplierRepo.DeleteAsync(s.Id);
                LoadData();
            }
        }

        private void ClearForm()
        {
            Name = ""; Phone = ""; Email = ""; Address = ""; Note = "";
            SelectedSupplier = null;
            IsEditMode = false;
        }
    }
}