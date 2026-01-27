using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;

namespace InventorySystem.UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        public RelayCommand NavigateToProductsCommand { get; }
        public RelayCommand NavigateToCategoriesCommand { get; }
        public RelayCommand NavigateToStockCommand { get; } // <--- 1. MAKE SURE THIS IS HERE
        public RelayCommand NavigateToPOSCommand { get; }

        public RelayCommand NavigateToReportsCommand { get; }

        public MainViewModel()
        {
            var db = DatabaseService.CreateDbContext();

            NavigateToProductsCommand = new RelayCommand(() =>
            {
                var repo = new ProductRepository(db);
                CurrentView = new ProductViewModel(repo);
            });

            NavigateToCategoriesCommand = new RelayCommand(() =>
            {
                var repo = new CategoryRepository(db);
                CurrentView = new CategoryViewModel(repo);
            });

            // 2. MAKE SURE THIS BLOCK IS HERE
            NavigateToStockCommand = new RelayCommand(() =>
            {
                var productRepo = new ProductRepository(db);
                var stockRepo = new StockRepository(db);
                CurrentView = new StockViewModel(productRepo, stockRepo);
            });

            NavigateToPOSCommand = new RelayCommand(() =>
            {
                var productRepo = new ProductRepository(db);
                var stockRepo = new StockRepository(db);
                CurrentView = new POSViewModel(productRepo, stockRepo);
            });

            NavigateToReportsCommand = new RelayCommand(() =>
            {
                var productRepo = new ProductRepository(db);
                var stockRepo = new StockRepository(db);
                CurrentView = new ReportsViewModel(stockRepo);
            });

            // Default view
            NavigateToProductsCommand.Execute(null);
        }
    }
}