using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Infrastructure.Startup;
using System.Linq; // Required for .Any()
using System.Windows;

namespace InventorySystem.UI
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Initialize Infrastructure (if you have this class)
            await AppInitializer.InitializeAsync();

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

            // 4. Show Main Window
            // Note: Since you are using manual injection in MainViewModel, 
            // the MainWindow XAML will automatically instantiate MainViewModel 
            // if you set it as DataContext in XAML or here.

            // If you removed StartupUri="MainWindow.xaml" from App.xaml, uncomment this:
            // var mainWindow = new MainWindow();
            // mainWindow.DataContext = new MainViewModel();
            // mainWindow.Show();
        }
    }
}