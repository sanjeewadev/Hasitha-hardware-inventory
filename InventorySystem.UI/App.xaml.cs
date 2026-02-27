using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.ViewModels;
using InventorySystem.UI.Views;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace InventorySystem.UI
{
    public partial class App : Application
    {
        private static Mutex? _mutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "H_and_J_Inventory_System_v1";
            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("The application is already open!", "H & J Inventory", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            base.OnStartup(e);

            var dbService = new DatabaseService();
            dbService.Initialize();

            using (var db = DatabaseService.CreateDbContext())
            {
            }

            _ = Task.Run(async () =>
            {
                var settingsVm = new SettingsViewModel();
                await settingsVm.CheckAndRunAutoBackup();
            });

            var dbContext = DatabaseService.CreateDbContext();
            var userRepo = new UserRepository(dbContext);
            var authService = new AuthenticationService(userRepo);
            var sessionManager = SessionManager.Instance;

            var loginVm = new LoginViewModel(authService, sessionManager);

            var loginWindow = new LoginWindow();
            loginWindow.DataContext = loginVm;

            loginVm.CloseAction = () =>
            {
                if (sessionManager.IsLoggedIn)
                {
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    loginWindow.Close();
                }
                else
                {
                    loginWindow.Close();
                    Shutdown();
                }
            };

            loginWindow.Show();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}\n\nIf this persists, please contact support.",
                            "System Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

            e.Handled = true;
        }
    }
}