using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;
using System.Windows; // Needed for Visibility

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

        // --- 1. Session Data for UI (Header Info) ---
        public string CurrentUserName => SessionManager.Instance.Username;
        public string CurrentUserRole => SessionManager.Instance.UserRoleDisplay;
        public string CurrentUserInitial => !string.IsNullOrEmpty(CurrentUserName) ? CurrentUserName.Substring(0, 1).ToUpper() : "?";

        // --- 2. The Gatekeeper Logic (Sidebar Visibility) ---
        // If IsAdmin is true, Visible. If false, Collapsed.
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
        public RelayCommand NavigateToUsersCommand { get; } // <--- Added This

        public MainViewModel()
        {
            // --- 3. Listen for Login/Logout ---
            SessionManager.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SessionManager.CurrentUser))
                {
                    // Refresh all UI properties when user changes
                    OnPropertyChanged(nameof(CurrentUserName));
                    OnPropertyChanged(nameof(CurrentUserRole));
                    OnPropertyChanged(nameof(CurrentUserInitial));
                    OnPropertyChanged(nameof(AdminVisibility));
                }
            };

            // Create the shared Database Context
            var db = DatabaseService.CreateDbContext();

            // Create Services for User Management
            var userRepo = new UserRepository(db);
            var authService = new AuthenticationService(userRepo);

            // --- Initialize Commands ---

            NavigateToProductsCommand = new RelayCommand(() =>
            {
                CurrentView = new ProductViewModel(new ProductRepository(db), new CategoryRepository(db), new StockRepository(db));
            });

            NavigateToInventoryCommand = new RelayCommand(() =>
            {
                CurrentView = new InventoryViewModel(new ProductRepository(db), new CategoryRepository(db), new StockRepository(db));
            });

            NavigateToStockCommand = new RelayCommand(() =>
            {
                CurrentView = new StockViewModel(new ProductRepository(db), new CategoryRepository(db), new StockRepository(db));
            });

            NavigateToPOSCommand = new RelayCommand(() =>
            {
                CurrentView = new POSViewModel(new ProductRepository(db), new StockRepository(db));
            });

            NavigateToDashboardCommand = new RelayCommand(() =>
            {
                CurrentView = new DashboardViewModel(new StockRepository(db), new ProductRepository(db));
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

            // --- 4. NEW: Navigate to User Manager ---
            NavigateToUsersCommand = new RelayCommand(() =>
            {
                CurrentView = new UsersViewModel(userRepo, authService);
            });

            // Default Startup View (POS is safe for everyone)
            NavigateToPOSCommand.Execute(null);
        }
    }
}