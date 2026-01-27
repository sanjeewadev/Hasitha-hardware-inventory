using System;

namespace InventorySystem.Core.Entities
{
    public class StockBatch
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        // FIFO Core Data
        public decimal CostPrice { get; set; }      // How much THIS specific batch cost
        public int InitialQuantity { get; set; }    // How many you bought originally
        public int RemainingQuantity { get; set; }  // How many are left in this batch (FIFO logic uses this)

        public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
    }
}