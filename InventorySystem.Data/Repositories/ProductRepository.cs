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

        // 1. GET ALL (Hide the deleted ones!)
        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            return await _context.Products
                .Include(p => p.Category)
                .ThenInclude(c => c.Parent) // <--- ADD THIS LINE (Loads the Parent info)
                .Where(p => !p.IsDeleted) // <--- CRITICAL FILTER
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
            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Product product)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        // 2. DELETE (Don't erase, just hide!)
        public async Task DeleteAsync(Product product)
        {
            // Instead of .Remove(product), we do this:
            product.IsDeleted = true;

            _context.Products.Update(product); // Save as "Updated", not deleted
            await _context.SaveChangesAsync();
        }
    }
}