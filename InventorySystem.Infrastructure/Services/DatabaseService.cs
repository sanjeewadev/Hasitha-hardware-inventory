using InventorySystem.Data.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace InventorySystem.Infrastructure.Services
{
    public class DatabaseService
    {
        // 1. Get the path to the DB file
        public static string GetDbPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inventory.db");
        }

        // 2. Helper to get the Connection String (Useful for your Repositories)
        public static string GetConnectionString()
        {
            return $"Data Source={GetDbPath()}";
        }

        // 3. Create the Context (Used by your app)
        public static InventoryDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
            optionsBuilder.UseSqlite(GetConnectionString());
            return new InventoryDbContext(optionsBuilder.Options);
        }

        // 4. NEW: Initialize the Database (Create Users Table)
        public void Initialize()
        {
            using (var context = CreateDbContext())
            {
                // This ensures the database file exists
                context.Database.EnsureCreated();

                // Create the Users table if it doesn't exist
                string createUserTable = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT UNIQUE NOT NULL,
                        PasswordHash TEXT NOT NULL,
                        Role INTEGER NOT NULL,
                        FullName TEXT,
                        IsActive INTEGER DEFAULT 1,
                        CreatedAt TEXT
                    );";

                // Execute the raw SQL
                context.Database.ExecuteSqlRaw(createUserTable);
            }
        }
    }
}