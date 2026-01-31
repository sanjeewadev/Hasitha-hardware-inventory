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

        // We don't need the list of categories for a dropdown anymore, 
        // but we keep it internally to lookup names.
        private ObservableCollection<Category> _allCategories = new();

        // NEW: Property to display "Main > Sub" as text
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

        // --- CONSTRUCTOR ---
        public AddProductViewModel(IProductRepository pRepo, ICategoryRepository cRepo, Product? productToEdit = null, int? preSelectedCategoryId = null)
        {
            _productRepo = pRepo;
            _categoryRepo = cRepo;

            // Initialize Commands
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
            GenerateCodeCommand = new RelayCommand(() => EditingProduct.Barcode = GenerateSimpleCode());

            if (productToEdit != null)
            {
                // EDIT MODE
                EditingProduct = new Product
                {
                    Id = productToEdit.Id,
                    Name = productToEdit.Name,
                    Barcode = productToEdit.Barcode,
                    Description = productToEdit.Description,
                    CategoryId = productToEdit.CategoryId,
                    Quantity = productToEdit.Quantity
                };
            }
            else
            {
                // NEW MODE
                EditingProduct = new Product();
                EditingProduct.Barcode = GenerateSimpleCode(); // Auto-fill Barcode

                if (preSelectedCategoryId.HasValue)
                {
                    EditingProduct.CategoryId = preSelectedCategoryId.Value;
                }
            }

            // Load category data to build the display path
            LoadCategoryPath();
        }

        private async void LoadCategoryPath()
        {
            var cats = await _categoryRepo.GetAllAsync();
            _allCategories.Clear();
            foreach (var c in cats) _allCategories.Add(c);

            // Find the category for this product
            var currentCat = _allCategories.FirstOrDefault(c => c.Id == EditingProduct.CategoryId);

            if (currentCat != null)
            {
                // Check if it has a parent to make a pretty path like "Tools > Hammers"
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

        private string GenerateSimpleCode()
        {
            var random = new Random();
            return random.Next(10000000, 99999999).ToString();
        }

        private async void Save()
        {
            // 1. Validation
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
                // 2. Database Action
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
                if (ex.InnerException != null && ex.InnerException.Message.Contains("UNIQUE"))
                {
                    MessageBox.Show($"The Barcode '{EditingProduct.Barcode}' is already taken!\n\nPlease click 'Auto' (⚡) to generate a new one.",
                                    "Duplicate Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Error saving product: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Cancel()
        {
            CloseAction?.Invoke();
        }
    }
}