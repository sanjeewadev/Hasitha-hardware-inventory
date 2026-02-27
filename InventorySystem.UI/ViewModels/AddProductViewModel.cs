using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class AddProductViewModel : ViewModelBase
    {
        private readonly IProductRepository _productRepo;
        private readonly ICategoryRepository _categoryRepo;

        public Product EditingProduct { get; set; }

        // Standardized Unit Options
        public ObservableCollection<string> UnitOptions { get; } = new ObservableCollection<string>
        {
            "Pcs", "Box", "Set", "Pair", // Count
            "Kg", "g", "mg",             // Weight
            "M", "Ft", "cm", "mm",       // Length
            "L", "ml"                    // Volume
        };

        private ObservableCollection<Category> _allCategories = new();

        private string _categoryDisplayPath = "Loading...";
        public string CategoryDisplayPath
        {
            get => _categoryDisplayPath;
            set { _categoryDisplayPath = value; OnPropertyChanged(); }
        }

        public Action? CloseAction { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand GenerateCodeCommand { get; }

        public AddProductViewModel(IProductRepository pRepo, ICategoryRepository cRepo, Product? productToEdit = null, int? preSelectedCategoryId = null)
        {
            _productRepo = pRepo;
            _categoryRepo = cRepo;

            SaveCommand = new RelayCommand(async () => await SaveAsync());
            CancelCommand = new RelayCommand(Cancel);
            GenerateCodeCommand = new RelayCommand(async () => await GenerateUniqueCodeAsync());

            if (productToEdit != null)
            {
                // EDIT MODE - Safe Copy (Protecting existing financials)
                EditingProduct = new Product
                {
                    Id = productToEdit.Id,
                    Name = productToEdit.Name,
                    Barcode = productToEdit.Barcode,
                    Description = productToEdit.Description,
                    CategoryId = productToEdit.CategoryId,
                    Quantity = productToEdit.Quantity,
                    Unit = productToEdit.Unit,
                    BuyingPrice = productToEdit.BuyingPrice,
                    SellingPrice = productToEdit.SellingPrice,
                    DiscountLimit = productToEdit.DiscountLimit
                };
            }
            else
            {
                // NEW MODE
                EditingProduct = new Product { Unit = "Pcs" };

                if (preSelectedCategoryId.HasValue)
                {
                    EditingProduct.CategoryId = preSelectedCategoryId.Value;
                }

                // Auto-generate a guaranteed unique code on startup
                _ = GenerateUniqueCodeAsync();
            }

            LoadCategoryPath();
        }

        private async void LoadCategoryPath()
        {
            var cats = await _categoryRepo.GetAllAsync();
            _allCategories.Clear();
            foreach (var c in cats) _allCategories.Add(c);

            var currentCat = _allCategories.FirstOrDefault(c => c.Id == EditingProduct.CategoryId);

            if (currentCat != null)
            {
                var parentCat = _allCategories.FirstOrDefault(c => c.Id == currentCat.ParentId);
                if (parentCat != null)
                    CategoryDisplayPath = $"{parentCat.Name} / {currentCat.Name}";
                else
                    CategoryDisplayPath = currentCat.Name;
            }
            else
            {
                CategoryDisplayPath = "Uncategorized";
            }
        }

        // FIX 2: Guaranteed Unique Barcode Generator
        private async Task GenerateUniqueCodeAsync()
        {
            var random = new Random();
            string newCode = "";
            bool isUnique = false;

            var allProducts = await _productRepo.GetAllAsync();

            while (!isUnique)
            {
                newCode = random.Next(10000000, 99999999).ToString();

                // Check if this random number already exists in the database
                if (!allProducts.Any(p => p.Barcode == newCode))
                {
                    isUnique = true;
                }
            }

            EditingProduct.Barcode = newCode;
            OnPropertyChanged(nameof(EditingProduct));
        }

        // FIX 1: Proactive Validation instead of Exception-Catching
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(EditingProduct.Name))
            {
                MessageBox.Show("Please enter a Product Name.", "Missing Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(EditingProduct.Barcode))
            {
                MessageBox.Show("Product must have a Barcode or SKU.", "Missing Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Proactive Duplicate Barcode Check
                var allProducts = await _productRepo.GetAllAsync();
                var duplicate = allProducts.FirstOrDefault(p =>
                    p.Barcode.Equals(EditingProduct.Barcode, StringComparison.OrdinalIgnoreCase) &&
                    p.Id != EditingProduct.Id); // Ignore ourselves if we are editing

                if (duplicate != null)
                {
                    MessageBox.Show($"The Barcode '{EditingProduct.Barcode}' is already assigned to '{duplicate.Name}'.\n\nPlease click 'Auto' to generate a new one, or type a unique code.", "Duplicate Barcode", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Database Action
                if (EditingProduct.Id == 0)
                {
                    await _productRepo.AddAsync(EditingProduct);
                }
                else
                {
                    await _productRepo.UpdateAsync(EditingProduct);
                }

                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred while saving: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel()
        {
            CloseAction?.Invoke();
        }
    }
}