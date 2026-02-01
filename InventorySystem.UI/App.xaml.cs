using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.ViewModels;
using InventorySystem.UI.Views;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading; // Needed for Global Exception Handling

namespace InventorySystem.UI
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            // 1. Hook up Global Error Handling (Prevents "Crash to Desktop")
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            base.OnStartup(e);

            // 2. Initialize Database (Creates tables if missing)
            var dbService = new DatabaseService();
            dbService.Initialize();

            // 3. Database & Seeding (Cleaned up for Production)
            // We no longer seed "Tools" or "Hardware" automatically. 
            // The user must create their own categories.
            using (var db = DatabaseService.CreateDbContext())
            {
                // You can add critical system data here if needed in the future,
                // but for now, we leave it empty so the client starts fresh.
            }

            // 4. Start Background Backup (Fire and Forget)
            _ = Task.Run(async () =>
            {
                var settingsVm = new SettingsViewModel();
                await settingsVm.CheckAndRunAutoBackup();
            });

            // 5. Prepare Login Dependencies
            var dbContext = DatabaseService.CreateDbContext();
            var userRepo = new UserRepository(dbContext);
            var authService = new AuthenticationService(userRepo);
            var sessionManager = SessionManager.Instance;

            var loginVm = new LoginViewModel(authService, sessionManager);

            // 6. Show Login Window
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