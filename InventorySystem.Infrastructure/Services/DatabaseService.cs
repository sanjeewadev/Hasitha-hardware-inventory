using InventorySystem.Data.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO; // Needed for Path

namespace InventorySystem.Infrastructure.Services
{
    public class DatabaseService
    {
        // Helper to get the path
        public static string GetDbPath()
        {
            // This forces the DB to be exactly where the .exe is running
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inventory.db");
        }

        public static InventoryDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();

            // USE THE FIXED PATH HERE
            optionsBuilder.UseSqlite($"Data Source={GetDbPath()}");

            return new InventoryDbContext(optionsBuilder.Options);
        }
    }
}