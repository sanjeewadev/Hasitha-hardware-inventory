using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace InventorySystem.Data.Context
{
    public class InventoryDbContext : DbContext
    {
        public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<StockMovement> StockMovements => Set<StockMovement>();
        public DbSet<StockBatch> StockBatches => Set<StockBatch>();

        // --- NEW TABLES FOR CREDIT SYSTEM ---
        public DbSet<SalesTransaction> SalesTransactions => Set<SalesTransaction>();
        public DbSet<CreditPaymentLog> CreditPaymentLogs => Set<CreditPaymentLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- 0. CRITICAL FIX: SQLite Decimal Conversion ---
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                {
                    var properties = entityType.ClrType.GetProperties()
                        .Where(p => p.PropertyType == typeof(decimal));

                    foreach (var property in properties)
                    {
                        modelBuilder.Entity(entityType.Name)
                            .Property(property.Name)
                            .HasConversion<double>();
                    }
                }
            }

            // 1. Seed Users
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    PasswordHash = "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9",
                    Role = UserRole.SuperAdmin,
                    FullName = "Super Admin",
                    IsActive = true,
                    CreatedAt = System.DateTime.Now
                }
            );

            // 2. Unique Barcode Index
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Barcode)
                .IsUnique();

            // Default Unit Value
            modelBuilder.Entity<Product>()
                .Property(p => p.Unit)
                .HasDefaultValue("Pcs");

            // 3. Cascade Deletes
            modelBuilder.Entity<Category>()
                .HasOne(c => c.Parent)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StockBatch>()
                .HasOne(b => b.Product)
                .WithMany(p => p.Batches)
                .HasForeignKey(b => b.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- 4. NEW: Credit System Configuration ---

            // Link Payment Logs to the main Transaction via ReceiptId
            modelBuilder.Entity<CreditPaymentLog>()
                .HasOne(l => l.SalesTransaction)
                .WithMany()
                .HasForeignKey(l => l.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            // 5. Precision Configuration
            modelBuilder.Entity<Product>().Property(p => p.BuyingPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.SellingPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.Quantity).HasPrecision(18, 3);

            modelBuilder.Entity<StockBatch>().Property(b => b.CostPrice).HasPrecision(18, 2);
            modelBuilder.Entity<StockBatch>().Property(b => b.InitialQuantity).HasPrecision(18, 3);
            modelBuilder.Entity<StockBatch>().Property(b => b.RemainingQuantity).HasPrecision(18, 3);

            modelBuilder.Entity<StockMovement>().Property(m => m.UnitCost).HasPrecision(18, 2);
            modelBuilder.Entity<StockMovement>().Property(m => m.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<StockMovement>().Property(m => m.Quantity).HasPrecision(18, 3);

            // New Tables Precision
            modelBuilder.Entity<SalesTransaction>().Property(t => t.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<SalesTransaction>().Property(t => t.PaidAmount).HasPrecision(18, 2);
            modelBuilder.Entity<CreditPaymentLog>().Property(l => l.AmountPaid).HasPrecision(18, 2);

            // --- IGNORE CALCULATED PROPERTIES (Fixes InvalidOperationException) ---
            modelBuilder.Entity<SalesTransaction>().Ignore(t => t.RemainingBalance);
            modelBuilder.Entity<StockMovement>().Ignore(m => m.LineTotal);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string dbPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "inventory.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }
    }
}