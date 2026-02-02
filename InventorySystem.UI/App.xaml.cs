using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.ViewModels;
using InventorySystem.UI.Views;
using System.Linq;
using System.Threading; // Needed for Mutex (Single Instance)
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading; // Needed for Global Exception Handling

namespace InventorySystem.UI
{
    public partial class App : Application
    {
        // Unique ID for your app to check if it's already running
        private static Mutex? _mutex = null;

        protected override async void OnStartup(StartupEventArgs e)
        {
            // --- 1. SINGLE INSTANCE CHECK (Prevent multiple windows) ---
            const string appName = "H_and_J_Inventory_System_v1";
            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // App is already running! Show message and close this new instance.
                MessageBox.Show("The application is already open!", "H & J Inventory", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }
            // -----------------------------------------------------------

            // 2. Hook up Global Error Handling (Prevents "Crash to Desktop")
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            base.OnStartup(e);

            // 3. Initialize Database (Creates tables if missing)
            var dbService = new DatabaseService();
            dbService.Initialize();

            // 4. Database & Seeding
            // We ensure critical data exists but don't force test categories anymore.
            using (var db = DatabaseService.CreateDbContext())
            {
                // Future system-critical seeding can go here.
            }

            // 5. Start Background Backup (Fire and Forget)
            _ = Task.Run(async () =>
            {
                var settingsVm = new SettingsViewModel();
                await settingsVm.CheckAndRunAutoBackup();
            });

            // 6. Prepare Login Dependencies
            var dbContext = DatabaseService.CreateDbContext();
            var userRepo = new UserRepository(dbContext);
            var authService = new AuthenticationService(userRepo);
            var sessionManager = SessionManager.Instance;

            var loginVm = new LoginViewModel(authService, sessionManager);

            // 7. Show Login Window
            var loginWindow = new LoginWindow();
            loginWindow.DataContext = loginVm;

            // Handle Login Flow
            loginVm.CloseAction = () =>
            {
                // Check success BEFORE closing the login window
                if (sessionManager.IsLoggedIn)
                {
                    // A. Open the Main Window FIRST
                    var mainWindow = new MainWindow();
                    mainWindow.Show();

                    // B. NOW close the Login Window
                    // (The app stays alive because MainWindow is open)
                    loginWindow.Close();
                }
                else
                {
                    // If simply closing without login, shut down
                    loginWindow.Close();
                    Shutdown();
                }
            };

            loginWindow.Show();
        }

        // --- GLOBAL ERROR HANDLER ---
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Log this error or show a friendly message
            MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}\n\nIf this persists, please contact support.",
                            "System Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

            // Prevent the app from crashing entirely
            e.Handled = true;
        }
    }
}