using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using System.Windows.Controls;
using InventorySystem.UI.ViewModels;

namespace InventorySystem.UI.Views
{
    public partial class ProductView : UserControl
    {
        public ProductView()
        {
            InitializeComponent();

            var db = DatabaseService.CreateDbContext();

            // Create ALL Repositories needed
            var productRepo = new ProductRepository(db);
            var categoryRepo = new CategoryRepository(db);
            var stockRepo = new StockRepository(db); // <--- NEW

            // Pass all 3 to the ViewModel
            DataContext = new ProductViewModel(productRepo, categoryRepo, stockRepo);
        }
    }
}