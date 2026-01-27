using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class ProductViewModel : ViewModelBase
    {
        private readonly IProductRepository _repo;

        public ObservableCollection<Product> Products { get; set; } = new();

        public Product SelectedProduct { get; set; }

        public ICommand OpenAddProductCommand { get; }
        public ICommand EditProductCommand { get; }
        public ICommand DeleteProductCommand { get; }

        public ProductViewModel(IProductRepository repo)
        {
            _repo = repo;

            LoadProducts();

            OpenAddProductCommand = new RelayCommand(OpenAddProduct);
            EditProductCommand = new RelayCommand(() => EditProduct(SelectedProduct));
            DeleteProductCommand = new RelayCommand(() => DeleteProduct(SelectedProduct));
        }

        private void LoadProducts()
        {
            Products.Clear();
            foreach (var p in _repo.GetAllAsync().Result)
                Products.Add(p);
        }

        private void OpenAddProduct()
        {
            var addWindow = new AddProductWindow();
            addWindow.ShowDialog();
            LoadProducts();
        }

        private void EditProduct(Product product)
        {
            if (product == null) return;

            // Open the AddProductWindow but pre-fill the fields
            var editWindow = new AddProductWindow();

            // Create an EditProductViewModel (inherits AddProductViewModel)
            var categoryRepo = new CategoryRepository(DatabaseService.CreateDbContext());
            var vm = new EditProductViewModel(product, _repo, categoryRepo, () => editWindow.Close());

            (editWindow.Content as InventorySystem.UI.Views.AddProductView)?.SetViewModel(vm);
            editWindow.ShowDialog();

            LoadProducts();
        }

        private void DeleteProduct(Product product)
        {
            if (product == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete {product.Name}?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _repo.DeleteAsync(product).Wait();
                LoadProducts();
            }
        }
    }
}