using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Infrastructure.Startup;
using InventorySystem.UI.ViewModels; // Added for SettingsViewModel
using System.Linq;
using System.Threading.Tasks; // Added for Task.Run
using System.Windows;

namespace InventorySystem.UI
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Initialize Infrastructure
            // await AppInitializer.InitializeAsync(); // Uncomment if you use this

            // 2. Create Database & Seed Data
            using var db = DatabaseService.CreateDbContext();
            await db.Database.EnsureCreatedAsync();

            var categoryRepo = new CategoryRepository(db);
            var categories = await categoryRepo.GetAllAsync();

            // 3. Seed Default Categories if empty
            if (!categories.Any())
            {
                await categoryRepo.AddAsync(new Category { Name = "Tools" });
                await categoryRepo.AddAsync(new Category { Name = "Hardware" });
            }

            // --- 4. START BACKGROUND BACKUP CHECK ---
            // We create a temporary Settings VM just to run the check logic.
            // Using Task.Run prevents this from slowing down the app launch.
            _ = Task.Run(async () =>
            {
                var settingsVm = new SettingsViewModel();
                await settingsVm.CheckAndRunAutoBackup();
            });

            // 5. Show Main Window
            // (Assuming MainWindow is set as StartupUri in App.xaml)
            // If not, uncomment below:
            // var mainWindow = new MainWindow();
            // mainWindow.Show();
        }
    }
}