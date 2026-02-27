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

        public async Task ProcessCompleteSaleAsync(SalesTransaction transaction, List<StockMovement> movements)
        {
            using var dbTrans = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.SalesTransactions.AddAsync(transaction);

                foreach (var move in movements)
                {
                    move.ReceiptId = transaction.ReceiptId;

                    var batch = await _context.StockBatches.FindAsync(move.StockBatchId);
                    if (batch == null) throw new InvalidOperationException("Fatal Error: Source stock batch not found.");

                    await _context.Entry(batch).ReloadAsync(); // FORCE FETCH LIVE DB VALUE

                    if (batch.RemainingQuantity < move.Quantity)
                        throw new InvalidOperationException($"Cart out of sync! Only {batch.RemainingQuantity} left in batch, but you tried to sell {move.Quantity}. Please clear cart and try again.");

                    batch.RemainingQuantity -= move.Quantity;
                    _context.StockBatches.Update(batch);

                    var product = await _context.Products.FindAsync(move.ProductId);
                    if (product == null) throw new InvalidOperationException("Fatal Error: Product catalog entry not found.");

                    await _context.Entry(product).ReloadAsync(); // FORCE FETCH LIVE DB VALUE

                    if (product.Quantity < move.Quantity)
                        throw new InvalidOperationException($"Cart out of sync! Total product stock is only {product.Quantity}. Please clear cart and try again.");

                    product.Quantity -= move.Quantity;
                    _context.Products.Update(product);

                    await _context.StockMovements.AddAsync(move);
                }

                await _context.SaveChangesAsync();
                await dbTrans.CommitAsync();
            }
            catch
            {
                await dbTrans.RollbackAsync();
                throw;
            }
        }

        public async Task ReceiveStockAsync(StockMovement movement)
        {
            var product = await _context.Products.FindAsync(movement.ProductId);
            if (product == null) return;

            await _context.Entry(product).ReloadAsync();

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

        public async Task SellStockAsync(StockMovement sale)
        {
            var product = await _context.Products.FindAsync(sale.ProductId);
            if (product == null) return;

            await _context.Entry(product).ReloadAsync();

            if (product.Quantity < sale.Quantity)
                throw new InvalidOperationException($"Insufficient stock. Available: {product.Quantity}");

            product.Quantity -= sale.Quantity;

            _context.StockMovements.Add(sale);
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task AdjustStockAsync(StockMovement adjustment)
        {
            var product = await _context.Products.FindAsync(adjustment.ProductId);
            if (product == null) return;

            if (adjustment.StockBatchId == null)
                throw new InvalidOperationException("Specific batch must be selected for adjustment.");

            var batch = await _context.StockBatches.FindAsync(adjustment.StockBatchId);
            if (batch == null) throw new InvalidOperationException("Selected batch not found.");

            await _context.Entry(batch).ReloadAsync();
            await _context.Entry(product).ReloadAsync();

            if (adjustment.Quantity > batch.RemainingQuantity)
                throw new InvalidOperationException($"Cannot remove {adjustment.Quantity} items. Batch only has {batch.RemainingQuantity}.");

            batch.RemainingQuantity -= adjustment.Quantity;
            _context.StockBatches.Update(batch);

            product.Quantity -= adjustment.Quantity;
            if (product.Quantity < 0) product.Quantity = 0;
            _context.Products.Update(product);

            adjustment.UnitPrice = 0;
            adjustment.UnitCost = adjustment.Reason == AdjustmentReason.Correction ? 0 : batch.CostPrice;
            adjustment.Type = StockMovementType.Adjustment;

            _context.StockMovements.Add(adjustment);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<StockBatch>> GetAllBatchesAsync() =>
            await _context.StockBatches.Include(b => b.Product).ToListAsync();

        public async Task<IEnumerable<StockBatch>> GetActiveBatchesAsync() =>
            await _context.StockBatches
                .Include(b => b.Product)
                .Include(b => b.PurchaseInvoice)
                .Where(b => b.RemainingQuantity > 0 &&
                            (b.PurchaseInvoiceId == null || b.PurchaseInvoice!.Status == InvoiceStatus.Posted))
                .ToListAsync();

        public async Task<IEnumerable<StockMovement>> GetHistoryAsync() =>
            await _context.StockMovements.Include(m => m.Product).OrderByDescending(m => m.Date).ToListAsync();

        public async Task AddStockBatchAsync(StockBatch batch) { await _context.StockBatches.AddAsync(batch); await _context.SaveChangesAsync(); }
        public async Task UpdateBatchAsync(StockBatch batch) { _context.StockBatches.Update(batch); await _context.SaveChangesAsync(); }
        public async Task DeleteBatchAsync(StockBatch batch) { _context.StockBatches.Remove(batch); await _context.SaveChangesAsync(); }

        public async Task<IEnumerable<StockMovement>> GetSalesHistoryAsync() =>
            await _context.StockMovements
                .Include(m => m.Product)
                .Where(m => m.Type == StockMovementType.Out || m.Type == StockMovementType.Adjustment || m.Type == StockMovementType.SalesReturn)
                .OrderByDescending(m => m.Date)
                .ToListAsync();

        public async Task<IEnumerable<StockMovement>> GetSalesByDateRangeAsync(DateTime start, DateTime end) =>
            await _context.StockMovements
                .Include(m => m.Product)
                .Where(m => (m.Type == StockMovementType.Out || m.Type == StockMovementType.Adjustment || m.Type == StockMovementType.SalesReturn)
                             && m.Date >= start && m.Date <= end)
                .OrderByDescending(m => m.Date)
                .ToListAsync();

        public async Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold) =>
            await _context.Products.Include(p => p.Category).Where(p => p.Quantity <= threshold).OrderBy(p => p.Quantity).ToListAsync();

        // --- SECURED VOID LOGIC ---
        public async Task VoidReceiptAsync(string receiptId)
        {
            using var dbTrans = await _context.Database.BeginTransactionAsync();
            try
            {
                var transaction = await _context.SalesTransactions.FirstOrDefaultAsync(t => t.ReceiptId == receiptId);
                if (transaction != null)
                {
                    if (transaction.IsCredit && transaction.PaidAmount > 0)
                    {
                        throw new InvalidOperationException("Cannot void a credit sale that already has partial payments recorded. You must use the Customer Return page.");
                    }
                }

                // 🚨 NEW SECURITY LOCK: Prevent Voiding a Returned Receipt 🚨
                var hasReturns = await _context.StockMovements.AnyAsync(m => m.ReceiptId == receiptId && m.Type == StockMovementType.SalesReturn);
                if (hasReturns)
                {
                    throw new InvalidOperationException("Action Blocked: This receipt contains Returned items.\n\nThe inventory has already been balanced via the Returns process. Voiding it now would corrupt the database.");
                }

                // Proceed with Void
                if (transaction != null)
                {
                    _context.SalesTransactions.Remove(transaction);
                }

                var movements = await _context.StockMovements
                    .Where(m => m.ReceiptId == receiptId && !m.IsVoided && m.Type == StockMovementType.Out)
                    .ToListAsync();

                if (!movements.Any()) return;

                foreach (var move in movements)
                {
                    move.IsVoided = true;
                    move.Note += " [VOIDED]";

                    var product = await _context.Products.FindAsync(move.ProductId);
                    if (product != null)
                    {
                        await _context.Entry(product).ReloadAsync();
                        product.Quantity += move.Quantity;
                        _context.Products.Update(product);
                    }

                    if (move.StockBatchId.HasValue)
                    {
                        var batch = await _context.StockBatches.FindAsync(move.StockBatchId.Value);
                        if (batch != null)
                        {
                            await _context.Entry(batch).ReloadAsync();
                            batch.RemainingQuantity += move.Quantity;
                            _context.StockBatches.Update(batch);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await dbTrans.CommitAsync();
            }
            catch
            {
                await dbTrans.RollbackAsync();
                throw;
            }
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

        public async Task<List<SalesTransaction>> GetTransactionsByReceiptIdsAsync(IEnumerable<string> receiptIds)
        {
            return await _context.SalesTransactions
                .Where(t => receiptIds.Contains(t.ReceiptId))
                .ToListAsync();
        }

        public async Task<IEnumerable<StockMovement>> GetVoidsAndReturnsAsync(DateTime start, DateTime end)
        {
            return await _context.StockMovements
                .Include(m => m.Product)
                .Where(m => (m.Type == StockMovementType.SalesReturn) || (m.Type == StockMovementType.Out && m.IsVoided))
                .Where(m => m.Date >= start && m.Date <= end)
                .OrderByDescending(m => m.Date)
                .ToListAsync();
        }
    }
}