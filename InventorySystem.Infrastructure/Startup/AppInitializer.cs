using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using System.Threading.Tasks;

namespace InventorySystem.Infrastructure.Startup
{
    public static class AppInitializer
    {
        public static async Task InitializeAsync()
        {
            using var db = DatabaseService.CreateDbContext();

            // Create DB & tables if not exist
            await db.Database.EnsureCreatedAsync();

            // Optional: seed initial data
            if (!db.Products.Any())
            {
                var productRepo = new ProductRepository(db);

                var product = new Product
                {
                    Name = "Hammer",
                    BuyingPrice = 100,
                    SellingPrice = 150,
                    Quantity = 10,
                    Category = new Category { Name = "Tools" }
                };

                await productRepo.AddAsync(product);
            }
        }
    }
}