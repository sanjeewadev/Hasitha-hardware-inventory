using InventorySystem.Core.Entities;
using InventorySystem.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace InventorySystem.UI.Views
{
    public partial class AdjustmentView : UserControl
    {
        public AdjustmentView()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Cast to the NEW ViewModel Name
            if (DataContext is AdjustmentViewModel vm)
            {
                vm.SelectedCategory = e.NewValue as Category;
            }
        }
    }
}