using InventorySystem.Core.Entities;
using InventorySystem.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace InventorySystem.UI.Views
{
    public partial class StockView : UserControl
    {
        public StockView()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is StockViewModel vm)
            {
                vm.SelectedCategory = e.NewValue as Category;
            }
        }
    }
}