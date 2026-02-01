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

        // --- NAVIGATION COMMANDS ---
        public RelayCommand NavigateToProductsCommand { get; }
        public RelayCommand NavigateToInventoryCommand { get; }
        public RelayCommand NavigateToStockCommand { get; }
        public RelayCommand NavigateToPOSCommand { get; }
        public RelayCommand NavigateToDashboardCommand { get; }
        public RelayCommand NavigateToHistoryCommand { get; }
        public RelayCommand NavigateToTodaySalesCommand { get; }
        public RelayCommand NavigateToSettingsCommand { get; }

        public MainViewModel()
        {
            var db = DatabaseService.CreateDbContext();

            // 1. Products
            NavigateToProductsCommand = new RelayCommand(() =>
            {
                CurrentView = new ProductViewModel(
                    new ProductRepository(db),
                    new CategoryRepository(db),
                    new StockRepository(db)
                );
            });

            // 2. Inventory Catalog
            NavigateToInventoryCommand = new RelayCommand(() =>
            {
                CurrentView = new InventoryViewModel(
                    new ProductRepository(db),
                    new CategoryRepository(db),
                    new StockRepository(db)
                );
            });

            // 3. Stock
            NavigateToStockCommand = new RelayCommand(() =>
            {
                CurrentView = new StockViewModel(
                    new ProductRepository(db),
                    new CategoryRepository(db),
                    new StockRepository(db)
                );
            });

            // 4. POS
            NavigateToPOSCommand = new RelayCommand(() =>
            {
                CurrentView = new POSViewModel(
                    new ProductRepository(db),
                    new StockRepository(db)
                );
            });

            // 5. Analytics & Dashboard (UPDATED)
            NavigateToDashboardCommand = new RelayCommand(() =>
            {
                // Now passing BOTH StockRepository and ProductRepository
                CurrentView = new DashboardViewModel(
                    new StockRepository(db),
                    new ProductRepository(db)
                );
            });

            NavigateToHistoryCommand = new RelayCommand(() =>
            {
                CurrentView = new SalesHistoryViewModel(new StockRepository(db));
            });

            NavigateToTodaySalesCommand = new RelayCommand(() =>
            {
                CurrentView = new TodaySalesViewModel(new StockRepository(db));
            });

            // 6. Settings
            NavigateToSettingsCommand = new RelayCommand(() =>
            {
                CurrentView = new SettingsViewModel();
            });

            // Default Startup View
            NavigateToPOSCommand.Execute(null); // Changed default to Dashboard (optional, usually better for admins)
        }
    }
}