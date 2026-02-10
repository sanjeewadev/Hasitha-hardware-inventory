using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;
using System.Windows;

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

        // --- Session Data ---
        public string CurrentUserName => SessionManager.Instance.Username;
        public string CurrentUserRole => SessionManager.Instance.UserRoleDisplay;
        public string CurrentUserInitial => !string.IsNullOrEmpty(CurrentUserName) ? CurrentUserName.Substring(0, 1).ToUpper() : "?";

        // --- Gatekeeper Logic ---
        public Visibility AdminVisibility => SessionManager.Instance.IsAdmin ? Visibility.Visible : Visibility.Collapsed;

        // --- NAVIGATION COMMANDS ---
        public RelayCommand NavigateToProductsCommand { get; }
        public RelayCommand NavigateToInventoryCommand { get; }
        public RelayCommand NavigateToStockCommand { get; }
        public RelayCommand NavigateToPOSCommand { get; }
        public RelayCommand NavigateToDashboardCommand { get; }
        public RelayCommand NavigateToHistoryCommand { get; }
        public RelayCommand NavigateToTodaySalesCommand { get; }
        public RelayCommand NavigateToSettingsCommand { get; }
        public RelayCommand NavigateToUsersCommand { get; }
        public RelayCommand NavigateToCreditsCommand { get; }

        // --- NEW COMMANDS ---
        public RelayCommand NavigateToSuppliersCommand { get; }
        public RelayCommand NavigateToStockInCommand { get; }

        public MainViewModel()
        {
            SessionManager.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SessionManager.CurrentUser))
                {
                    OnPropertyChanged(nameof(CurrentUserName));
                    OnPropertyChanged(nameof(CurrentUserRole));
                    OnPropertyChanged(nameof(CurrentUserInitial));
                    OnPropertyChanged(nameof(AdminVisibility));
                }
            };

            var db = DatabaseService.CreateDbContext();

            // Repositories
            var userRepo = new UserRepository(db);
            var productRepo = new ProductRepository(db);
            var categoryRepo = new CategoryRepository(db);
            var stockRepo = new StockRepository(db);
            var authService = new AuthenticationService(userRepo);
            var creditService = new CreditService(db);

            // --- Commands ---

            NavigateToProductsCommand = new RelayCommand(() =>
            {
                CurrentView = new ProductViewModel(productRepo, categoryRepo, stockRepo);
            });

            NavigateToInventoryCommand = new RelayCommand(() =>
            {
                CurrentView = new InventoryViewModel(productRepo, categoryRepo, stockRepo);
            });

            NavigateToStockCommand = new RelayCommand(() =>
            {
                CurrentView = new StockViewModel(productRepo, categoryRepo, stockRepo);
            });

            NavigateToPOSCommand = new RelayCommand(() =>
            {
                CurrentView = new POSViewModel(productRepo, stockRepo);
            });

            NavigateToDashboardCommand = new RelayCommand(() =>
            {
                CurrentView = new DashboardViewModel(stockRepo, productRepo);
            });

            NavigateToHistoryCommand = new RelayCommand(() =>
            {
                CurrentView = new SalesHistoryViewModel(stockRepo);
            });

            NavigateToTodaySalesCommand = new RelayCommand(() =>
            {
                CurrentView = new TodaySalesViewModel(stockRepo);
            });

            NavigateToSettingsCommand = new RelayCommand(() =>
            {
                CurrentView = new SettingsViewModel();
            });

            NavigateToUsersCommand = new RelayCommand(() =>
            {
                CurrentView = new UsersViewModel(userRepo, authService);
            });

            NavigateToCreditsCommand = new RelayCommand(() =>
            {
                CurrentView = new CreditManagerViewModel(creditService);
            });

            // --- NEW NAVIGATION LOGIC ---
            NavigateToSuppliersCommand = new RelayCommand(() =>
            {
                // Supplier View handles its own repository inside the VM for now
                CurrentView = new SupplierViewModel();
            });

            NavigateToStockInCommand = new RelayCommand(() =>
            {
                // Stock In handles its own logic
                CurrentView = new StockInViewModel();
            });

            // Default Startup
            NavigateToPOSCommand.Execute(null);
        }
    }
}