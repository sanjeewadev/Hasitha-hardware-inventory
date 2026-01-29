using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

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

            // 1. Seed Users
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Username = "admin", Password = "123", Role = UserRole.Admin },
                new User { Id = 2, Username = "user", Password = "123", Role = UserRole.Employee }
            );

            // 2. Unique Barcode Index
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Barcode)
                .IsUnique();

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

            // 4. Precision
            modelBuilder.Entity<Product>().Property(p => p.BuyingPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.SellingPrice).HasPrecision(18, 2);
            modelBuilder.Entity<StockBatch>().Property(b => b.CostPrice).HasPrecision(18, 2);
            modelBuilder.Entity<StockMovement>().Property(m => m.UnitCost).HasPrecision(18, 2);
            modelBuilder.Entity<StockMovement>().Property(m => m.UnitPrice).HasPrecision(18, 2);
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