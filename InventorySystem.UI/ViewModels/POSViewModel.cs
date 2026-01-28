using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq; // Needed for Sum()
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    // 1. Smart Cart Item (Handles its own math)
    public class CartItem : ViewModelBase
    {
        public Product Product { get; set; } = null!;

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPrice)); // Recalculate if Qty changes
                ParentViewModel?.CalculateTotal();     // Tell Main VM to update Grand Total
            }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                _unitPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPrice)); // Recalculate if Price changes
                ParentViewModel?.CalculateTotal();     // Tell Main VM to update Grand Total
            }
        }

        public decimal TotalPrice => Quantity * UnitPrice;

        // Reference to parent so we can trigger updates
        public POSViewModel? ParentViewModel { get; set; }
    }

    public class POSViewModel : ViewModelBase
    {
        private readonly IProductRepository _productRepo;
        private readonly IStockRepository _stockRepo;

        // --- DATA ---
        private System.Collections.Generic.List<Product> _allProductsCache = new();
        public ObservableCollection<Product> FilteredProducts { get; } = new();
        public ObservableCollection<CartItem> Cart { get; } = new();

        // --- SEARCH ---
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); FilterList(); }
        }

        // --- TOTALS ---
        private decimal _grandTotal;
        public decimal GrandTotal
        {
            get => _grandTotal;
            set { _grandTotal = value; OnPropertyChanged(); }
        }

        // --- COMMANDS ---
        public ICommand AddToCartCommand { get; }
        public ICommand RemoveFromCartCommand { get; }
        public ICommand CheckoutCommand { get; }

        public POSViewModel(IProductRepository productRepo, IStockRepository stockRepo)
        {
            _productRepo = productRepo;
            _stockRepo = stockRepo;

            AddToCartCommand = new RelayCommand<Product>(AddToCart);
            RemoveFromCartCommand = new RelayCommand<CartItem>(RemoveFromCart);
            CheckoutCommand = new RelayCommand(async () => await CheckoutAsync());

            LoadProducts();
        }

        private async void LoadProducts()
        {
            var list = await _productRepo.GetAllAsync();
            _allProductsCache = list.ToList();
            FilterList();
        }

        private void FilterList()
        {
            FilteredProducts.Clear();
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                foreach (var p in _allProductsCache) FilteredProducts.Add(p);
            }
            else
            {
                var lower = SearchText.ToLower();
                foreach (var p in _allProductsCache.Where(p => p.Name.ToLower().Contains(lower)))
                    FilteredProducts.Add(p);
            }
        }

        private void AddToCart(Product product)
        {
            if (product == null) return;

            // Check if already in cart
            var existing = Cart.FirstOrDefault(c => c.Product.Id == product.Id);
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                // Create new item
                var item = new CartItem
                {
                    Product = product,
                    Quantity = 1,
                    UnitPrice = product.SellingPrice, // Default to Selling Price
                    ParentViewModel = this
                };
                Cart.Add(item);
            }
            CalculateTotal();
        }

        private void RemoveFromCart(CartItem item)
        {
            Cart.Remove(item);
            CalculateTotal();
        }

        public void CalculateTotal()
        {
            GrandTotal = Cart.Sum(c => c.TotalPrice);
        }

        private async Task CheckoutAsync()
        {
            if (Cart.Count == 0) { MessageBox.Show("Cart is empty!"); return; }

            // 1. Process Sales in Database
            foreach (var item in Cart)
            {
                var sale = new StockMovement
                {
                    ProductId = item.Product.Id,
                    Quantity = item.Quantity,
                    Type = StockMovementType.Out,
                    Date = DateTime.UtcNow,
                    Note = $"Sale @ {item.UnitPrice:N2}", // Record the price sold at
                    // --- NEW: SAVE THE FINANCIALS ---
                    UnitPrice = item.UnitPrice,           // What the customer paid
                    UnitCost = item.Product.BuyingPrice   // What it cost you
                };
                await _stockRepo.SellStockAsync(sale);
            }

            MessageBox.Show($"Payment Successful!\nTotal: {GrandTotal:N2}");

            // 2. Clear Cart
            Cart.Clear();
            CalculateTotal();

            // 3. Refresh Stock Levels (Reload products to see new quantities)
            LoadProducts();
        }
    }
}