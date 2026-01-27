using System.Collections.Generic;

namespace InventorySystem.Core.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        // This is the "Current Market Price" or "Last Price Paid"
        public decimal BuyingPrice { get; set; }

        public decimal SellingPrice { get; set; }

        // CACHED TOTAL: This must always equal Sum(Batches.RemainingQuantity)
        public int Quantity { get; set; }

        public bool IsActive { get; set; } = true;

        // NEW: Link to the batches
        public ICollection<StockBatch> Batches { get; set; } = new List<StockBatch>();
    }
}