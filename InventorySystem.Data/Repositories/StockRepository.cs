using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using InventorySystem.Data.Context;
using Microsoft.EntityFrameworkCore;
using System;
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

        // --- NEW: MASTER TRANSACTION LOGIC (POS V2.0) ---
        public async Task ProcessCompleteSaleAsync(SalesTransaction transaction, List<StockMovement> movements)
        {
            // 1. Start a Database Transaction (Safety Net)
            // If anything fails (e.g., Internet cuts, error), NOTHING is saved.
            using var dbTrans = await _context.Database.BeginTransactionAsync();

            try
            {
                // A. Save the Receipt Header (Money/Credit Info)
                await _context.SalesTransactions.AddAsync(transaction);

                // B. Process Every Item in the Cart
                foreach (var move in movements)
                {
                    // 1. Link the item to the Receipt
                    move.ReceiptId = transaction.ReceiptId;

                    // 2. Deduct Physical Stock from the Batch
                    // We load the batch fresh from DB to ensure numbers are correct
                    var batch = await _context.StockBatches.FindAsync(move.StockBatchId);
                    if (batch != null)
                    {
                        // Deduct from the specific batch
                        batch.RemainingQuantity -= move.Quantity;

                        // Safety check (optional): Ensure we don't go negative
                        // if (batch.RemainingQuantity < 0) throw new Exception($"Batch #{batch.Id} has insufficient stock.");

                        _context.StockBatches.Update(batch);
                    }

                    // 3. Deduct from the Main Product Total (Quick View)
                    var product = await _context.Products.FindAsync(move.ProductId);
                    if (product != null)
                    {
                        product.Quantity -= move.Quantity;
                        _context.Products.Update(product);
                    }

                    // 4. Add the movement record log
                    await _context.StockMovements.AddAsync(move);
                }

                // C. Save Everything to DB
                await _context.SaveChangesAsync();

                // D. Commit (Make it permanent)
                await dbTrans.CommitAsync();
            }
            catch
            {
                // If error, Undo everything
                await dbTrans.RollbackAsync();
                throw; // Send error back to POS to show user
            }
        }

        // --- 1. RECEIVE STOCK ---
        public async Task ReceiveStockAsync(StockMovement movement)
        {
            var product = await _context.Products.FindAsync(movement.ProductId);
            if (product == null) return;

            product.Quantity += movement.Quantity;
            _context.StockMovements.Add(movement);

            var batch = new StockBatch
            {
                ProductId = movement.ProductId,
                InitialQuantity = movement.Quantity,
                RemainingQuantity = movement.Quantity,
                CostPrice = movement.UnitCost,
                ReceivedDate = movement.Date,
                SellingPrice = product.SellingPrice,
                Discount = product.DiscountLimit,
                DiscountCode = GenerateSimpleCode(product.DiscountLimit)
            };

            _context.StockBatches.Add(batch);
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        // --- 2. SELL STOCK (Legacy / Single Item Logic) ---
        public async Task SellStockAsync(StockMovement sale)
        {
            var product = await _context.Products.FindAsync(sale.ProductId);
            if (product == null) return;

            if (product.Quantity < sale.Quantity)
                throw new InvalidOperationException($"Insufficient stock. Available: {product.Quantity}");

            product.Quantity -= sale.Quantity;

            _context.StockMovements.Add(sale);
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        // --- 3. ADJUST STOCK (SMART PROFIT LOGIC) ---
        public async Task AdjustStockAsync(StockMovement adjustment)
        {
            var product = await _context.Products.FindAsync(adjustment.ProductId);
            if (product == null) return;

            // A. Validate Target Batch
            if (adjustment.StockBatchId == null)
                throw new InvalidOperationException("Specific batch must be selected for adjustment.");

            var batch = await _context.StockBatches.FindAsync(adjustment.StockBatchId);
            if (batch == null) throw new InvalidOperationException("Selected batch not found.");

            if (adjustment.Quantity > batch.RemainingQuantity)
                throw new InvalidOperationException($"Cannot remove {adjustment.Quantity} items. Batch only has {batch.RemainingQuantity}.");

            // B. Deduct from Batch
            batch.RemainingQuantity -= adjustment.Quantity;
            _context.StockBatches.Update(batch);

            // C. Deduct from Main Product
            product.Quantity -= adjustment.Quantity;
            if (product.Quantity < 0) product.Quantity = 0;
            _context.Products.Update(product);

            // D. FINANCIAL LOGIC
            adjustment.UnitPrice = 0; // Revenue is 0 for adjustments

            if (adjustment.Reason == AdjustmentReason.Correction)
            {
                // Correction: No financial impact.
                adjustment.UnitCost = 0;
            }
            else
            {
                // Lost / Damaged / Theft: Real financial loss.
                adjustment.UnitCost = batch.CostPrice;
            }

            adjustment.Type = StockMovementType.Adjustment;
            _context.StockMovements.Add(adjustment);

            await _context.SaveChangesAsync();
        }

        // --- HELPERS ---
        public async Task<IEnumerable<StockBatch>> GetAllBatchesAsync() =>
            await _context.StockBatches.Include(b => b.Product).ToListAsync();
        // --- SECURED: Only fetches batches that have NO invoice, or a POSTED invoice ---
        public async Task<IEnumerable<StockBatch>> GetActiveBatchesAsync() =>
            await _context.StockBatches
                .Include(b => b.Product)
                .Include(b => b.PurchaseInvoice) // Include invoice to check status
                .Where(b => b.RemainingQuantity > 0 &&
                           (b.PurchaseInvoiceId == null || b.PurchaseInvoice!.Status == InvoiceStatus.Posted))
                .ToListAsync();

        public async Task<IEnumerable<StockMovement>> GetHistoryAsync() =>
            await _context.StockMovements.Include(m => m.Product).OrderByDescending(m => m.Date).ToListAsync();

        public async Task AddStockBatchAsync(StockBatch batch) { await _context.StockBatches.AddAsync(batch); await _context.SaveChangesAsync(); }
        public async Task UpdateBatchAsync(StockBatch batch) { _context.StockBatches.Update(batch); await _context.SaveChangesAsync(); }
        public async Task DeleteBatchAsync(StockBatch batch) { _context.StockBatches.Remove(batch); await _context.SaveChangesAsync(); }

        // --- REPORTING ---
        public async Task<IEnumerable<StockMovement>> GetSalesHistoryAsync() =>
            await _context.StockMovements
                .Include(m => m.Product)
                .Where(m => m.Type == StockMovementType.Out || m.Type == StockMovementType.Adjustment)
                .OrderByDescending(m => m.Date)
                .ToListAsync();

        public async Task<IEnumerable<StockMovement>> GetSalesByDateRangeAsync(DateTime start, DateTime end) =>
            await _context.StockMovements
                .Include(m => m.Product)
                .Where(m => (m.Type == StockMovementType.Out || m.Type == StockMovementType.Adjustment) && m.Date >= start && m.Date <= end)
                .OrderByDescending(m => m.Date)
                .ToListAsync();

        public async Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold) =>
            await _context.Products.Include(p => p.Category).Where(p => p.Quantity <= threshold).OrderBy(p => p.Quantity).ToListAsync();

        // --- VOID LOGIC ---
        public async Task VoidReceiptAsync(string receiptId)
        {
            var movements = await _context.StockMovements.Where(m => m.ReceiptId == receiptId && !m.IsVoided).ToListAsync();
            if (!movements.Any()) return;

            foreach (var move in movements)
            {
                move.IsVoided = true;
                move.Note += " [VOIDED]";

                var product = await _context.Products.FindAsync(move.ProductId);
                if (product != null)
                {
                    product.Quantity += move.Quantity;
                    _context.Products.Update(product);
                }

                if (move.StockBatchId.HasValue)
                {
                    var batch = await _context.StockBatches.FindAsync(move.StockBatchId.Value);
                    if (batch != null)
                    {
                        batch.RemainingQuantity += move.Quantity;
                        _context.StockBatches.Update(batch);
                    }
                }
            }
            await _context.SaveChangesAsync();
        }
        
        public async Task VoidSaleAsync(int movementId, string reason)
        {
            var sale = await _context.StockMovements.FindAsync(movementId);
            if (sale != null && !sale.IsVoided && !string.IsNullOrEmpty(sale.ReceiptId))
            {
                await VoidReceiptAsync(sale.ReceiptId);
            }
        }

        private string GenerateSimpleCode(decimal discount)
        {
            var rnd = new Random();
            return $"{rnd.Next(0, 9)}{(int)discount:000}{rnd.Next(0, 9)}";
        }
    }
}