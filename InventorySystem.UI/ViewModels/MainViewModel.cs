using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;
using System.Windows;
using System.Windows.Input;

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

        // --- PERMISSION GATEKEEPER LOGIC ---
        private bool HasPermission(string targetPermission)
        {
            if (SessionManager.Instance.IsAdmin) return true; // Admins always see everything

            var currentUser = SessionManager.Instance.CurrentUser;
            if (currentUser == null) return false;

            var userPerms = currentUser.Permissions ?? "";
            return userPerms.Contains("ALL") || userPerms.Contains(targetPermission);
        }

        // --- DYNAMIC VISIBILITY PROPERTIES ---
        public Visibility PosVisibility => HasPermission("POS") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility TodaySalesVisibility => HasPermission("TodaySales") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CreditVisibility => HasPermission("Credit") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ReturnsVisibility => HasPermission("Returns") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility SuppliersVisibility => HasPermission("Suppliers") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility StockAdjustVisibility => HasPermission("StockAdjust") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ProductsVisibility => HasPermission("Products") ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ReportsVisibility => HasPermission("Reports") ? Visibility.Visible : Visibility.Collapsed;

        // Admin Only Pages
        public Visibility AdminVisibility => SessionManager.Instance.IsAdmin ? Visibility.Visible : Visibility.Collapsed;

        // Group Header Visibilities
        public Visibility StockSectionVisibility => (HasPermission("Suppliers") || HasPermission("StockAdjust")) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ManagementSectionVisibility => (HasPermission("Reports") || SessionManager.Instance.IsAdmin) ? Visibility.Visible : Visibility.Collapsed;

        // --- NAVIGATION COMMANDS ---
        public RelayCommand NavigateToProductsCommand { get; }
        public RelayCommand NavigateToInventoryCommand { get; }
        public RelayCommand NavigateToStockCommand { get; }
        public RelayCommand NavigateToPOSCommand { get; }
        public RelayCommand NavigateToDashboardCommand { get; }
        public RelayCommand NavigateToHistoryCommand { get; }
        public ICommand NavigateToSalesReturnCommand { get; }
        public RelayCommand NavigateToTodaySalesCommand { get; }
        public RelayCommand NavigateToSettingsCommand { get; }
        public RelayCommand NavigateToUsersCommand { get; }
        public RelayCommand NavigateToCreditsCommand { get; }
        public RelayCommand NavigateToSuppliersCommand { get; }
        public RelayCommand NavigateToStockInCommand { get; }
        public RelayCommand NavigateToAuditLogCommand { get; }

        public MainViewModel()
        {
            SessionManager.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SessionManager.CurrentUser))
                {
                    OnPropertyChanged(nameof(CurrentUserName));
                    OnPropertyChanged(nameof(CurrentUserRole));
                    OnPropertyChanged(nameof(CurrentUserInitial));

                    // Refresh ALL visibilities when a user logs in
                    OnPropertyChanged(nameof(AdminVisibility));
                    OnPropertyChanged(nameof(PosVisibility));
                    OnPropertyChanged(nameof(TodaySalesVisibility));
                    OnPropertyChanged(nameof(CreditVisibility));
                    OnPropertyChanged(nameof(ReturnsVisibility));
                    OnPropertyChanged(nameof(SuppliersVisibility));
                    OnPropertyChanged(nameof(StockAdjustVisibility));
                    OnPropertyChanged(nameof(ProductsVisibility));
                    OnPropertyChanged(nameof(ReportsVisibility));
                    OnPropertyChanged(nameof(StockSectionVisibility));
                    OnPropertyChanged(nameof(ManagementSectionVisibility));
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
            NavigateToProductsCommand = new RelayCommand(() => CurrentView = new ProductViewModel(productRepo, categoryRepo, stockRepo));
            NavigateToInventoryCommand = new RelayCommand(() => CurrentView = new InventoryViewModel(productRepo, categoryRepo, stockRepo));
            NavigateToStockCommand = new RelayCommand(() => CurrentView = new AdjustmentViewModel(productRepo, categoryRepo, stockRepo));
            NavigateToPOSCommand = new RelayCommand(() => CurrentView = new POSViewModel());
            NavigateToDashboardCommand = new RelayCommand(() => CurrentView = new DashboardViewModel(stockRepo));
            NavigateToHistoryCommand = new RelayCommand(() => CurrentView = new SalesHistoryViewModel(stockRepo));
            NavigateToSalesReturnCommand = new RelayCommand(() => CurrentView = new SalesReturnViewModel());
            NavigateToTodaySalesCommand = new RelayCommand(() => CurrentView = new TodaySalesViewModel(stockRepo));
            NavigateToSettingsCommand = new RelayCommand(() => CurrentView = new SettingsViewModel());
            NavigateToUsersCommand = new RelayCommand(() => CurrentView = new UsersViewModel(userRepo, authService));
            NavigateToCreditsCommand = new RelayCommand(() => CurrentView = new CreditManagerViewModel(creditService));
            NavigateToSuppliersCommand = new RelayCommand(() => CurrentView = new SupplierViewModel());
            NavigateToStockInCommand = new RelayCommand(() => CurrentView = new StockInViewModel());
            NavigateToAuditLogCommand = new RelayCommand(() => CurrentView = new AuditLogViewModel(stockRepo));

            // Default Startup
            NavigateToPOSCommand.Execute(null);
        }
    }
}