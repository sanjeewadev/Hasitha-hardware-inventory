using InventorySystem.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Data.Context
{
    public class InventoryDbContext : DbContext
    {
        public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
            : base(options)
        {
        }

        // DbSets
        public DbSet<User> Users => Set<User>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<StockMovement> StockMovements => Set<StockMovement>();
        public DbSet<StockBatch> StockBatches => Set<StockBatch>();

        // Relationships & configurations
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Category parent-child
            // NEW: Category Self-Referencing Relationship
            modelBuilder.Entity<Category>()
                .HasOne(c => c.Parent)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict); // Don't delete Parent if it has children!

            // Price precision
            modelBuilder.Entity<Product>()
                .Property(p => p.BuyingPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .Property(p => p.SellingPrice)
                .HasPrecision(18, 2);

            // Configure StockBatch Relationship
            modelBuilder.Entity<StockBatch>()
                .HasOne(b => b.Product)
                .WithMany(p => p.Batches)
                .HasForeignKey(b => b.ProductId)
                .OnDelete(DeleteBehavior.Cascade); // If Product deleted, batches go too
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Force the same path logic here too
                string dbPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "inventory.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }
    }
}