using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
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
    // Helper class for the Cart
    public class CartItem : ViewModelBase
    {
        public Product Product { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice => Product.SellingPrice * Quantity;
    }

    public class POSViewModel : ViewModelBase
    {
        private readonly IProductRepository _productRepo;
        private readonly IStockRepository _stockRepo;

        // The list of products to choose from
        public ObservableCollection<Product> AvailableProducts { get; } = new();

        // The Receipt/Cart
        public ObservableCollection<CartItem> Cart { get; } = new();

        // Total Bill Amount
        private decimal _grandTotal;
        public decimal GrandTotal
        {
            get => _grandTotal;
            set { _grandTotal = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand AddToCartCommand { get; }
        public ICommand RemoveFromCartCommand { get; }
        public ICommand CheckoutCommand { get; }

        public POSViewModel(IProductRepository productRepo, IStockRepository stockRepo)
        {
            _productRepo = productRepo;
            _stockRepo = stockRepo;

            LoadProducts();

            // Logic: Click a product -> Add to Cart
            AddToCartCommand = new RelayCommand<Product>(product =>
            {
                if (product == null) return;

                var existingItem = Cart.FirstOrDefault(c => c.Product.Id == product.Id);
                if (existingItem != null)
                {
                    existingItem.Quantity++;
                    // Trigger update for TotalPrice in UI
                    var index = Cart.IndexOf(existingItem);
                    Cart.RemoveAt(index);
                    Cart.Insert(index, existingItem);
                }
                else
                {
                    Cart.Add(new CartItem { Product = product, Quantity = 1 });
                }
                CalculateTotal();
            });

            // Logic: Remove item
            RemoveFromCartCommand = new RelayCommand<CartItem>(item =>
            {
                Cart.Remove(item);
                CalculateTotal();
            });

            // Logic: PAY button
            CheckoutCommand = new RelayCommand(async () => await ProcessCheckout());
        }

        private void LoadProducts()
        {
            AvailableProducts.Clear();
            var list = _productRepo.GetAllAsync().Result;
            foreach (var p in list) AvailableProducts.Add(p);
        }

        private void CalculateTotal()
        {
            GrandTotal = Cart.Sum(c => c.TotalPrice);
        }

        private async Task ProcessCheckout()
        {
            if (Cart.Count == 0) return;

            try
            {
                foreach (var item in Cart)
                {
                    var movement = new StockMovement
                    {
                        ProductId = item.Product.Id,
                        Quantity = item.Quantity,
                        Type = StockMovementType.Out, // Sale
                        Date = DateTime.UtcNow
                    };
                    await _stockRepo.SellStockAsync(movement);
                }

                MessageBox.Show($"Sale Complete! Total: {GrandTotal:C}");
                Cart.Clear();
                CalculateTotal();
                LoadProducts(); // Refresh stock counts
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
    }
}