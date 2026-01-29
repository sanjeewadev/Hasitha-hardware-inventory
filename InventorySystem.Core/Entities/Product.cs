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

        // --- PRICING DECISIONS ---
        public decimal BuyingPrice { get; set; } // Last Buying Price (Reference)
        public decimal SellingPrice { get; set; } // Current Shelf Price
        public double DiscountLimit { get; set; } // NEW: Max Discount % (e.g., 10%)

        public int Quantity { get; set; }
        public int LowStockThreshold { get; set; } = 5;
        public bool IsActive { get; set; } = true;

        public ICollection<StockBatch> Batches { get; set; } = new List<StockBatch>();
    }
}