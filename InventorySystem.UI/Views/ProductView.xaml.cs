using System.Windows.Controls;

namespace InventorySystem.UI.Views
{
    public partial class ProductView : UserControl
    {
        public ProductView()
        {
            InitializeComponent();
            // DataContext is now set by MainViewModel, preventing double-loading.
        }
    }
}