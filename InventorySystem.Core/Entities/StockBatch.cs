using System;

namespace InventorySystem.Core.Entities
{
    public class StockBatch
    {
        public int Id { get; set; }

        // Link to the Product
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        // FIFO Logic
        public int InitialQuantity { get; set; }    // How many we bought (e.g., 10)
        public int RemainingQuantity { get; set; }  // How many represent unsold stock (e.g., 4)

        public decimal CostPrice { get; set; }      // FIFO Cost (e.g., 450)

        public DateTime ReceivedDate { get; set; } = DateTime.Now;
    }
}