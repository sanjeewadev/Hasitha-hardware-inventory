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
        public DbSet<SalesTransaction> SalesTransactions => Set<SalesTransaction>();
        public DbSet<CreditPaymentLog> CreditPaymentLogs => Set<CreditPaymentLog>();

        // --- NEW TABLES ---
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- 0. SQLite Decimal Fix ---
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                {
                    var properties = entityType.ClrType.GetProperties()
                        .Where(p => p.PropertyType == typeof(decimal));

                    foreach (var property in properties)
                    {
                        modelBuilder.Entity(entityType.Name).Property(property.Name).HasConversion<double>();
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

            // 2. Unique Indexes
            modelBuilder.Entity<Product>().HasIndex(p => p.Barcode).IsUnique();
            modelBuilder.Entity<Supplier>().HasIndex(s => s.Name).IsUnique(); // Prevent duplicate Suppliers

            // Default Values
            modelBuilder.Entity<Product>().Property(p => p.Unit).HasDefaultValue("Pcs");

            // 3. Relationships (Cascade Deletes)
            modelBuilder.Entity<Category>().HasOne(c => c.Parent).WithMany(c => c.SubCategories).HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Product>().HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<StockBatch>().HasOne(b => b.Product).WithMany(p => p.Batches).HasForeignKey(b => b.ProductId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CreditPaymentLog>().HasOne(l => l.SalesTransaction).WithMany().HasForeignKey(l => l.ReceiptId).OnDelete(DeleteBehavior.Cascade);

            // --- 4. NEW: Supplier & Invoice Relationships ---

            // One Supplier -> Many Invoices (If Supplier deleted, invoices stay? Usually Restricted, but Cascade for now)
            modelBuilder.Entity<PurchaseInvoice>()
                .HasOne(i => i.Supplier)
                .WithMany(s => s.Invoices)
                .HasForeignKey(i => i.SupplierId)
                .OnDelete(DeleteBehavior.Restrict); // Safety: Don't accidentally delete a supplier with bills

            // One Invoice -> Many Batches (If Invoice deleted, batches deleted)
            modelBuilder.Entity<StockBatch>()
                .HasOne(b => b.PurchaseInvoice)
                .WithMany(i => i.Batches)
                .HasForeignKey(b => b.PurchaseInvoiceId)
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
            modelBuilder.Entity<SalesTransaction>().Property(t => t.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<SalesTransaction>().Property(t => t.PaidAmount).HasPrecision(18, 2);
            modelBuilder.Entity<CreditPaymentLog>().Property(l => l.AmountPaid).HasPrecision(18, 2);

            // New Table Precision
            modelBuilder.Entity<PurchaseInvoice>().Property(i => i.TotalAmount).HasPrecision(18, 2);

            // --- IGNORE CALCULATED PROPERTIES ---
            modelBuilder.Entity<SalesTransaction>().Ignore(t => t.RemainingBalance);
            modelBuilder.Entity<StockMovement>().Ignore(m => m.LineTotal);
            modelBuilder.Entity<StockBatch>().Ignore(b => b.MinSellingPrice);

            // --- FIX: Tell EF Core to ignore our new Math property ---
            modelBuilder.Entity<StockBatch>().Ignore(b => b.TotalLineCost);
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