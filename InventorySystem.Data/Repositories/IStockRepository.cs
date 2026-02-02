using InventorySystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventorySystem.Data.Repositories
{
    public interface IStockRepository
    {
        // --- NEW: The Master Transaction Method (Replaces simple SellStock loops) ---
        Task ProcessCompleteSaleAsync(SalesTransaction transaction, List<StockMovement> movements);

        // Core Actions
        Task ReceiveStockAsync(StockMovement movement);
        Task SellStockAsync(StockMovement sale); // Kept for legacy/single item support
        Task AdjustStockAsync(StockMovement adjustment);

        // Data Retrieval
        Task<IEnumerable<StockBatch>> GetAllBatchesAsync();
        Task<IEnumerable<StockBatch>> GetActiveBatchesAsync();
        Task<IEnumerable<StockMovement>> GetHistoryAsync();

        // Batch Management
        Task AddStockBatchAsync(StockBatch batch);
        Task UpdateBatchAsync(StockBatch batch);
        Task DeleteBatchAsync(StockBatch batch);

        // Reports
        Task<IEnumerable<StockMovement>> GetSalesHistoryAsync();
        Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold);
        Task<IEnumerable<StockMovement>> GetSalesByDateRangeAsync(DateTime start, DateTime end);

        // Voiding
        Task VoidReceiptAsync(string receiptId);
        Task VoidSaleAsync(int movementId, string reason);
    }
}