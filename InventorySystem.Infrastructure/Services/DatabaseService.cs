using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventorySystem.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Services
{
    public static class DatabaseService
    {
        public static InventoryDbContext CreateDbContext()
        {
            var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InventorySystem"
            );


            Directory.CreateDirectory(folder);


            var dbPath = Path.Combine(folder, "inventory.db");


            var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;


            return new InventoryDbContext(options);
        }
    }
}
