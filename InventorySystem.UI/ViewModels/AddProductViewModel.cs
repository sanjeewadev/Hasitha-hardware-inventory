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
        private readonly Action _closeWindowAction;

        // --- Product Fields ---
        private string _name = "";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private decimal _buyingPrice;
        public decimal BuyingPrice
        {
            get => _buyingPrice;
            set { _buyingPrice = value; OnPropertyChanged(); }
        }

        private decimal _sellingPrice;
        public decimal SellingPrice
        {
            get => _sellingPrice;
            set { _sellingPrice = value; OnPropertyChanged(); }
        }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set { _quantity = value; OnPropertyChanged(); }
        }

        // --- Categories ---
        public ObservableCollection<Category> Categories { get; } = new();
        private Category? _selectedCategory;
        public Category? SelectedCategory
        {
            get => _selectedCategory;
            set { _selectedCategory = value; OnPropertyChanged(); }
        }

        // --- Commands ---
        public ICommand SaveCommand { get; protected set; }

        public AddProductViewModel(
            IProductRepository productRepo,
            ICategoryRepository categoryRepo,
            Action closeWindowAction)
        {
            _productRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            _categoryRepo = categoryRepo ?? throw new ArgumentNullException(nameof(categoryRepo));
            _closeWindowAction = closeWindowAction ?? throw new ArgumentNullException(nameof(closeWindowAction));

            // Load categories initially
            LoadCategories();

            // Subscribe to category changes
            _categoryRepo.CategoriesChanged += LoadCategories;

            // Initialize Save command
            SaveCommand = new RelayCommand(async () => await SaveProductAsync());
        }

        private void LoadCategories()
        {
            Categories.Clear();
            var categories = _categoryRepo.GetAllAsync().Result; // Can be replaced with async pattern
            foreach (var c in categories)
                Categories.Add(c);

            SelectedCategory = Categories.FirstOrDefault();
        }

        private async Task SaveProductAsync()
        {
            // --- Validation ---
            if (string.IsNullOrWhiteSpace(Name))
            {
                MessageBox.Show("Name cannot be empty!");
                return;
            }

            if (BuyingPrice <= 0 || SellingPrice <= 0)
            {
                MessageBox.Show("Prices must be greater than 0!");
                return;
            }

            if (Quantity < 0)
            {
                MessageBox.Show("Quantity cannot be negative!");
                return;
            }

            if (SelectedCategory == null)
            {
                MessageBox.Show("Please select a category!");
                return;
            }

            // --- Create Product ---
            var product = new Product
            {
                Name = Name,
                BuyingPrice = BuyingPrice,
                SellingPrice = SellingPrice,
                Quantity = Quantity,
                Category = SelectedCategory
            };

            try
            {
                await _productRepo.AddAsync(product);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving product: {ex.Message}");
                return;
            }

            // --- Close Window ---
            _closeWindowAction();
        }
    }
}