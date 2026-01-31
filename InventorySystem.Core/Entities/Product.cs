using System.Collections.Generic;

namespace InventorySystem.Core.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Barcode { get; set; } = string.Empty;
        public string? Description { get; set; }

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        // Pricing
        public decimal BuyingPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public double DiscountLimit { get; set; }

        public int Quantity { get; set; }
        public int LowStockThreshold { get; set; } = 5;

        // --- NEW: THE SAFETY FLAG ---
        public bool IsDeleted { get; set; } = false;

        public ICollection<StockBatch> Batches { get; set; } = new List<StockBatch>();
    }
}