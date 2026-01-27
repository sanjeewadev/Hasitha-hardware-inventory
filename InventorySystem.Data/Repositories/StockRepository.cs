using InventorySystem.Core.Entities;
using InventorySystem.Data.Context;
using Microsoft.EntityFrameworkCore;
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

        public async Task ReceiveStockAsync(StockBatch batch)
        {
            // 1. Add the new Batch
            _context.StockBatches.Add(batch);

            // 2. Update the Main Product Totals (Cache)
            var product = await _context.Products.FindAsync(batch.ProductId);
            if (product != null)
            {
                // Increase total quantity
                product.Quantity += batch.InitialQuantity;

                // Update "Market Price" to the latest batch price
                product.BuyingPrice = batch.CostPrice;

                _context.Products.Update(product);
            }

            // 3. Save everything in one transaction
            await _context.SaveChangesAsync();
        }
        public async Task<IEnumerable<StockBatch>> GetAllBatchesAsync()
        {
            return await _context.StockBatches
                .Include(b => b.Product) // Load Product Name
                .OrderByDescending(b => b.ReceivedDate) // Newest first
                .ToListAsync();
        }

        public async Task SellStockAsync(StockMovement saleRecord)
        {
            // 1. Get the product
            var product = await _context.Products.FindAsync(saleRecord.ProductId);
            if (product == null) throw new Exception("Product not found");

            // 2. Check stock
            if (product.Quantity < saleRecord.Quantity)
                throw new Exception($"Not enough stock! Only {product.Quantity} left.");

            // 3. Deduct Stock
            product.Quantity -= saleRecord.Quantity;

            // 4. Record the Movement
            _context.StockMovements.Add(saleRecord);

            // 5. Update DB
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<StockMovement>> GetSalesHistoryAsync()
        {
            return await _context.StockMovements
                .Include(m => m.Product) // Load Product details
                .Where(m => m.Type == Core.Enums.StockMovementType.Out) // Only Sales
                .OrderByDescending(m => m.Date)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold)
        {
            return await _context.Products
                .Where(p => p.Quantity <= threshold)
                .ToListAsync();
        }

        public async Task AdjustStockAsync(StockMovement adjustment)
        {
            var product = await _context.Products.FindAsync(adjustment.ProductId);
            if (product == null) throw new Exception("Product not found");

            if (product.Quantity < adjustment.Quantity)
                throw new Exception("Cannot remove more items than you have in stock!");

            // Deduct Stock
            product.Quantity -= adjustment.Quantity;

            // Record the reason (Damaged, Stolen, etc.)
            // ensure your StockMovement.Type is set to 'Adjustment'
            _context.StockMovements.Add(adjustment);

            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }
    }
}