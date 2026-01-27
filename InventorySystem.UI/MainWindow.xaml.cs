using InventorySystem.UI.ViewModels;
using System.Windows;

namespace InventorySystem.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Connect the View to the ViewModel
            this.DataContext = new MainViewModel();
        }
    }
}