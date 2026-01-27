using InventorySystem.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventorySystem.Data.Repositories
{
    public interface IStockRepository
    {
        Task ReceiveStockAsync(StockBatch batch);
        Task<IEnumerable<StockBatch>> GetAllBatchesAsync();
        Task SellStockAsync(StockMovement saleRecord);
        Task AdjustStockAsync(StockMovement adjustment);

        // NEW METHODS
        Task<IEnumerable<StockMovement>> GetSalesHistoryAsync();
        Task<IEnumerable<Product>> GetLowStockProductsAsync(int threshold);
    }
}