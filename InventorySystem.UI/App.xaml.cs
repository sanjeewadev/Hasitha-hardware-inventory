using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.Infrastructure.Startup;
using System.Windows;

namespace InventorySystem.UI
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);


            await AppInitializer.InitializeAsync();


            // TODO: open MainWindow later
            using var db = DatabaseService.CreateDbContext();
            await db.Database.EnsureCreatedAsync();


            var categoryRepo = new CategoryRepository(db);
            var categories = await categoryRepo.GetAllAsync();


            if (!categories.Any())
            {
                await categoryRepo.AddAsync(new InventorySystem.Core.Entities.Category { Name = "Tools" });
                await categoryRepo.AddAsync(new InventorySystem.Core.Entities.Category { Name = "Hardware" });
            }
        }
    }
}