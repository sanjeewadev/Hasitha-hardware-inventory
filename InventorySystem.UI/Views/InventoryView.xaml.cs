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

        // --- THIS IS THE CRITICAL LOGIC ---
        // This method runs whenever you click a folder in the TreeView.
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // 1. Get the ViewModel connected to this page
            if (DataContext is InventoryViewModel vm)
            {
                // 2. Check if the clicked item is actually a Category
                if (e.NewValue is Category selectedCategory)
                {
                    // 3. Tell the ViewModel: "The user selected this category!"
                    // This triggers the product list to reload.
                    vm.SelectedCategory = selectedCategory;
                }
                else
                {
                    // If selection is cleared or invalid
                    vm.SelectedCategory = null;
                }
            }
        }
    }
}