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
        public RelayCommand NavigateToInventoryCommand { get; }
        public RelayCommand NavigateToStockCommand { get; }
        public RelayCommand NavigateToPOSCommand { get; }
        public RelayCommand NavigateToReportsCommand { get; }
        public RelayCommand NavigateToDashboardCommand { get; }
        public RelayCommand NavigateToHistoryCommand { get; }
        public RelayCommand NavigateToTodaySalesCommand { get; }
        public RelayCommand NavigateToSettingsCommand { get; }

        public MainViewModel()
        {
            var db = DatabaseService.CreateDbContext();

            // 1. FIXED: Create both repos here
            NavigateToProductsCommand = new RelayCommand(() =>
            {
                var productRepo = new ProductRepository(db);
                var categoryRepo = new CategoryRepository(db);
                var stockRepo = new StockRepository(db); // <--- NEW

                // Pass all 3
                CurrentView = new ProductViewModel(productRepo, categoryRepo, stockRepo);
            });

            // 2. FIXED: Create both repos here
            NavigateToInventoryCommand = new RelayCommand(() =>
            {
                var productRepo = new ProductRepository(db);
                var categoryRepo = new CategoryRepository(db);
                CurrentView = new InventoryViewModel(productRepo, categoryRepo);
            });

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

            NavigateToDashboardCommand = new RelayCommand(() =>
            {
                CurrentView = new DashboardViewModel(new StockRepository(db));
            });

            NavigateToHistoryCommand = new RelayCommand(() =>
            {
                CurrentView = new SalesHistoryViewModel(new StockRepository(db));
            });

            NavigateToTodaySalesCommand = new RelayCommand(() =>
            {
                CurrentView = new TodaySalesViewModel(new StockRepository(db));
            });

            NavigateToSettingsCommand = new RelayCommand(() =>
            {
                CurrentView = new SettingsViewModel();
            });

            // Default view
            NavigateToProductsCommand.Execute(null);
        }
    }
}