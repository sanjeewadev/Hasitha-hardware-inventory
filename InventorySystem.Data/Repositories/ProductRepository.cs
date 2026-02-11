using InventorySystem.Core.Entities;
using InventorySystem.Data.Context;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventorySystem.Data.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly InventoryDbContext _context;

        public ProductRepository(InventoryDbContext context)
        {
            _context = context;
        }

        // 1. GET ALL (Hide the inactive ones!)
        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            return await _context.Products
                .Include(p => p.Category)
                //.ThenInclude(c => c.Parent) // Uncomment if needed
                .Where(p => p.IsActive) // Only load Active products
                .ToListAsync();
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task AddAsync(Product product)
        {
            product.IsActive = true; // Ensure new products are visible
            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();
        }

        // --- FIX VULNERABILITY 3: SAFE UPDATE ---
        public async Task UpdateAsync(Product product)
        {
            // 1. Fetch the LIVE version from the database
            var existing = await _context.Products.FindAsync(product.Id);

            if (existing != null)
            {
                // 2. Manually copy ONLY the definition fields
                existing.Name = product.Name;
                existing.Barcode = product.Barcode;
                existing.Description = product.Description;
                existing.CategoryId = product.CategoryId;

                // NEW: Make sure we save the Unit (Kg, Pcs, etc.)
                existing.Unit = product.Unit;

                existing.BuyingPrice = product.BuyingPrice;
                existing.SellingPrice = product.SellingPrice;
                existing.DiscountLimit = product.DiscountLimit;

                // DELETED: LowStockThreshold was removed here to fix the crash.

                existing.IsActive = product.IsActive;

                // 3. CRITICAL: WE IGNORE 'product.Quantity'
                // The database quantity stays exactly as it is (safe from overwrites).
                await _context.SaveChangesAsync();
            }
        }

        // 2. DELETE (Soft Delete)
        public async Task DeleteAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                // Soft Delete: Mark as Inactive
                product.IsActive = false;

                // Optional: Mangle barcode so it can be reused later
                product.Barcode = $"{product.Barcode}_DEL_{System.DateTime.Now.Ticks}";

                _context.Products.Update(product);
                await _context.SaveChangesAsync();
            }
        }
    }
}