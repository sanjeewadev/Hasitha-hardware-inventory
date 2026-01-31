using InventorySystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventorySystem.Data.Repositories
{
    public interface IStockRepository
    {
        // 1. Core Stock Actions
        Task ReceiveStockAsync(StockMovement movement);
        Task SellStockAsync(StockMovement saleRecord);
        Task AdjustStockAsync(StockMovement adjustment);

        // 2. Data Retrieval
        Task<IEnumerable<StockBatch>> GetAllBatchesAsync();
        Task<IEnumerable<StockMovement>> GetHistoryAsync();

        // 3. Batch Management
        Task AddStockBatchAsync(StockBatch batch);

        // --- THESE WERE MISSING ---
        Task UpdateBatchAsync(StockBatch batch);
        Task DeleteBatchAsync(StockBatch batch);

        // 4. Reports & Analytics
        Task<IEnumerable<StockMovement>> GetSalesHistoryAsync();
        Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold);
        Task<IEnumerable<StockMovement>> GetSalesByDateRangeAsync(DateTime start, DateTime end);
        Task VoidSaleAsync(int movementId, string reason);
    }
}