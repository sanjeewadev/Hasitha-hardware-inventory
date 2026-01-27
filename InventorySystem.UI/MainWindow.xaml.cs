using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace InventorySystem.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var db = DatabaseService.CreateDbContext();
            var categoryRepo = new CategoryRepository(db);


            // Set DataContext for CategoryView
            CategoryViewControl.DataContext = new CategoryViewModel(categoryRepo);


            // ProductView DataContext
            var productRepo = new ProductRepository(db);
            ProductViewControl.DataContext = new ProductViewModel(productRepo);
        }

        private void ProductView_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}