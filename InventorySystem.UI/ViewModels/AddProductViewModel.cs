using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System;
using System.Collections.Generic;
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
        private readonly Product? _editingProduct;
        private List<Category> _allCategoriesRaw = new();

        // This holds the ID of the folder we are adding to
        private int _targetCategoryId;

        // --- UI PROPERTIES ---
        public string WindowTitle { get; set; }
        public string Name { get; set; } = "";

        // NEW: This shows "Tools / Hammers" instead of a dropdown
        public string CategoryPath { get; set; } = "Loading...";

        public bool IsEditMode => _editingProduct != null;

        private string _barcode = "";
        public string Barcode
        {
            get => _barcode;
            set { _barcode = value; OnPropertyChanged(); }
        }

        public string Description { get; set; } = "";

        public Action? CloseAction { get; set; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand GenerateBarcodeCommand { get; }

        // --- CONSTRUCTOR ---
        public AddProductViewModel(IProductRepository productRepo, ICategoryRepository categoryRepo, Product? productToEdit = null, int? preSelectedCategoryId = null)
        {
            _productRepo = productRepo;
            _categoryRepo = categoryRepo;
            _editingProduct = productToEdit;

            // Set the target category ID
            if (_editingProduct != null) _targetCategoryId = _editingProduct.CategoryId;
            else if (preSelectedCategoryId.HasValue) _targetCategoryId = preSelectedCategoryId.Value;

            SaveCommand = new RelayCommand(async () => await SaveAsync());
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
            GenerateBarcodeCommand = new RelayCommand(() => GenerateSmartSku());

            // Load Data
            InitializeData();

            if (_editingProduct != null)
            {
                WindowTitle = "Edit Product Identity";
                Name = _editingProduct.Name;
                Barcode = _editingProduct.Barcode ?? "";
                Description = _editingProduct.Description ?? "";
            }
            else
            {
                WindowTitle = "Create New Product Identity";
            }
        }

        private async void InitializeData()
        {
            // 1. Load all categories to build the path
            var cats = await _categoryRepo.GetAllAsync();
            _allCategoriesRaw = cats.ToList();

            // 2. Find the target category
            var targetCat = _allCategoriesRaw.FirstOrDefault(c => c.Id == _targetCategoryId);

            if (targetCat != null)
            {
                // 3. Build the Path String (e.g. "Tools / Hammers")
                CategoryPath = GetFullPath(targetCat);
                OnPropertyChanged(nameof(CategoryPath));

                // 4. Generate Code automatically if adding new
                if (_editingProduct == null)
                {
                    GenerateSmartSku(targetCat);
                }
            }
        }

        private string GetFullPath(Category cat)
        {
            if (cat.ParentId == null) return cat.Name;
            var parent = _allCategoriesRaw.FirstOrDefault(c => c.Id == cat.ParentId);
            return parent != null ? $"{GetFullPath(parent)} / {cat.Name}" : cat.Name;
        }

        // --- THE "SMART SKU" LOGIC ---
        private void GenerateSmartSku(Category? cat = null)
        {
            // Use the target category if none passed
            if (cat == null)
                cat = _allCategoriesRaw.FirstOrDefault(c => c.Id == _targetCategoryId);

            if (cat == null) return;

            string currentCode = GetShortCode(cat.Name);
            string parentCode = "";

            if (cat.ParentId != null)
            {
                var parent = _allCategoriesRaw.FirstOrDefault(c => c.Id == cat.ParentId);
                if (parent != null)
                {
                    parentCode = GetShortCode(parent.Name) + "-";
                }
            }

            string number = DateTime.Now.ToString("mmss");
            Barcode = $"{parentCode}{currentCode}-{number}";
        }

        private string GetShortCode(string name)
        {
            if (string.IsNullOrEmpty(name)) return "GEN";
            return name.Substring(0, Math.Min(3, name.Length)).ToUpper();
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name)) { MessageBox.Show("Name Required"); return; }

            string finalBarcode = string.IsNullOrWhiteSpace(Barcode)
                ? $"AUTO-{Guid.NewGuid().ToString().Substring(0, 6)}"
                : Barcode;

            if (_editingProduct == null)
            {
                var newProduct = new Product
                {
                    Name = Name,
                    CategoryId = _targetCategoryId, // Use the fixed ID
                    Barcode = finalBarcode,
                    SellingPrice = 0,
                    BuyingPrice = 0,
                    Quantity = 0,
                    Description = Description
                };
                await _productRepo.AddAsync(newProduct);
            }
            else
            {
                _editingProduct.Name = Name;
                // Category usually doesn't change in simple edit, but if it did, we'd update it here.
                _editingProduct.CategoryId = _targetCategoryId;
                _editingProduct.Barcode = finalBarcode;
                _editingProduct.Description = Description;
                await _productRepo.UpdateAsync(_editingProduct);
            }

            CloseAction?.Invoke();
        }
    }
}