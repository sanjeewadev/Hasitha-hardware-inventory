// AddProductWindow.xaml.cs
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.ViewModels;
using System.Windows;

namespace InventorySystem.UI
{
    public partial class AddProductWindow : Window
    {
        public AddProductWindow()
        {
            InitializeComponent();

            var db = DatabaseService.CreateDbContext();
            var productRepo = new ProductRepository(db);
            var categoryRepo = new CategoryRepository(db);

            var vm = new AddProductViewModel(productRepo, categoryRepo, () => this.Close());

            (this.Content as InventorySystem.UI.Views.AddProductView)?.SetViewModel(vm);
        }
    }
}