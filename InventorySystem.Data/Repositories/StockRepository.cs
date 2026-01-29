using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums; // Ensure you have this for StockMovementType
using InventorySystem.Data.Context;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventorySystem.Data.Repositories
{
    public class StockRepository : IStockRepository
    {
        private readonly InventoryDbContext _context;

        public StockRepository(InventoryDbContext context)
        {
            _context = context;
        }

        // --- 1. CORE ACTIONS ---

        public async Task ReceiveStockAsync(StockMovement movement)
        {
            var product = await _context.Products.FindAsync(movement.ProductId);
            if (product == null) return;

            // Update Product
            product.Quantity += movement.Quantity;

            // Add Movement Record
            _context.StockMovements.Add(movement);

            // Create the Batch Record (The "Branch" logic)
            var batch = new StockBatch
            {
                ProductId = movement.ProductId,
                InitialQuantity = movement.Quantity,
                RemainingQuantity = movement.Quantity,
                CostPrice = movement.UnitCost, // Use the exact cost saved in the movement
                ReceivedDate = movement.Date,

            };
            _context.StockBatches.Add(batch);

            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task SellStockAsync(StockMovement sale)
        {
            var product = await _context.Products.FindAsync(sale.ProductId);
            if (product == null) return;

            product.Quantity -= sale.Quantity;
            _context.StockMovements.Add(sale);
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task AdjustStockAsync(StockMovement adjustment)
        {
            var product = await _context.Products.FindAsync(adjustment.ProductId);
            if (product == null) return;

            product.Quantity -= adjustment.Quantity;
            if (product.Quantity < 0) product.Quantity = 0;

            _context.StockMovements.Add(adjustment);
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        // --- 2. DATA RETRIEVAL ---

        public async Task<IEnumerable<StockBatch>> GetAllBatchesAsync()
        {
            return await _context.StockBatches.ToListAsync();
        }

        public async Task<IEnumerable<StockMovement>> GetHistoryAsync()
        {
            return await _context.StockMovements
                .Include(m => m.Product)
                .OrderByDescending(m => m.Date)
                .ToListAsync();
        }

        // --- 3. REPORT METHODS (Implemented!) ---

        public async Task<IEnumerable<StockMovement>> GetSalesHistoryAsync()
        {
            // Filter only "Out" movements (Sales)
            return await _context.StockMovements
                .Include(m => m.Product)
                .Where(m => m.Type == StockMovementType.Out)
                .OrderByDescending(m => m.Date)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold)
        {
            // Find products where Quantity is below the threshold (e.g., 5)
            return await _context.Products
                .Include(p => p.Category)
                .Where(p => p.Quantity <= threshold)
                .OrderBy(p => p.Quantity)
                .ToListAsync();
        }

        public async Task<IEnumerable<StockMovement>> GetSalesByDateRangeAsync(DateTime start, DateTime end)
        {
            return await _context.StockMovements
                .Include(m => m.Product)
                .Where(m => m.Type == StockMovementType.Out && m.Date >= start && m.Date <= end)
                .OrderByDescending(m => m.Date)
                .ToListAsync();
        }

        public async Task VoidSaleAsync(int movementId, string reason)
        {
            var sale = await _context.StockMovements.FindAsync(movementId);
            if (sale == null) return;
            if (sale.IsVoided) return; // Already voided

            // 1. Mark as Voided
            sale.IsVoided = true;
            sale.Note += $" [VOIDED: {reason}]"; // Append reason to history

            // 2. RESTORE Stock to Product
            var product = await _context.Products.FindAsync(sale.ProductId);
            if (product != null)
            {
                product.Quantity += sale.Quantity; // Put items back on shelf
                _context.Products.Update(product);
            }

            _context.StockMovements.Update(sale);
            await _context.SaveChangesAsync();
        }

        // --- THE MISSING METHOD ---
        public async Task AddStockBatchAsync(StockBatch batch)
        {
            await _context.StockBatches.AddAsync(batch);
            await _context.SaveChangesAsync();
        }
    }
}