using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion; // Needed for the fix
using System.Linq; // Needed for LINQ queries

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- 0. CRITICAL FIX: SQLite Decimal Conversion ---
            // SQLite doesn't support decimal natively. It stores them as strings (TEXT) by default.
            // This breaks sorting (e.g., "10.0" comes before "2.0").
            // We force it to store as DOUBLE (REAL) for accurate sorting and math.
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
                            .HasConversion<double>(); // Store as REAL in SQLite
                    }
                }
            }

            // 1. Seed Users
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Username = "admin", Password = "123", Role = UserRole.Admin },
                new User { Id = 2, Username = "user", Password = "123", Role = UserRole.Employee }
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

            // 4. Precision (Still good to keep for other DB providers)
            modelBuilder.Entity<Product>().Property(p => p.BuyingPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.SellingPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.Quantity).HasPrecision(18, 3); // Support 3 decimals (e.g. 1.500 kg)

            modelBuilder.Entity<StockBatch>().Property(b => b.CostPrice).HasPrecision(18, 2);
            modelBuilder.Entity<StockBatch>().Property(b => b.InitialQuantity).HasPrecision(18, 3);
            modelBuilder.Entity<StockBatch>().Property(b => b.RemainingQuantity).HasPrecision(18, 3);

            modelBuilder.Entity<StockMovement>().Property(m => m.UnitCost).HasPrecision(18, 2);
            modelBuilder.Entity<StockMovement>().Property(m => m.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<StockMovement>().Property(m => m.Quantity).HasPrecision(18, 3);
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