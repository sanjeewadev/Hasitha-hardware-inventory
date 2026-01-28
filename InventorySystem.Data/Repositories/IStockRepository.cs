using InventorySystem.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventorySystem.Data.Repositories
{
    public interface IStockRepository
    {
        // 1. Core Stock Actions (Must match what ViewModel sends!)
        Task ReceiveStockAsync(StockMovement movement); // <--- FIXED: Changed from StockBatch to StockMovement
        Task SellStockAsync(StockMovement saleRecord);
        Task AdjustStockAsync(StockMovement adjustment);

        // 2. Data Retrieval
        Task<IEnumerable<StockBatch>> GetAllBatchesAsync();
        Task<IEnumerable<StockMovement>> GetHistoryAsync();

        // 3. New Report Methods (You added these, so we must implement them!)
        Task<IEnumerable<StockMovement>> GetSalesHistoryAsync();
        Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold);

        Task<IEnumerable<StockMovement>> GetSalesByDateRangeAsync(DateTime start, DateTime end);
        Task VoidSaleAsync(int movementId, string reason);
    }
}