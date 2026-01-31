using InventorySystem.Core.Entities;
using InventorySystem.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace InventorySystem.UI.Views
{
    public partial class InventoryView : UserControl
    {
        public InventoryView()
        {
            InitializeComponent();
        }

        // This handles the Single Click Selection on the TreeView
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is InventoryViewModel vm)
            {
                vm.SelectedCategory = e.NewValue as Category;
            }
        }
    }
}