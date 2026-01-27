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

            // Create DB context + repository
            var db = DatabaseService.CreateDbContext();
            var repo = new ProductRepository(db);

            // Set DataContext
            DataContext = new ProductViewModel(repo);
        }
    }
}