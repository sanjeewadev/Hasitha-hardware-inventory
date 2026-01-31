using System;

namespace InventorySystem.Core.Entities
{
    public class StockBatch
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int InitialQuantity { get; set; }
        public int RemainingQuantity { get; set; }

        // --- PRICING PER BATCH ---
        public decimal CostPrice { get; set; }       // Buying Price
        public decimal SellingPrice { get; set; }    // Selling Price
        public double Discount { get; set; }         // Discount %

        // NEW: The Owner-Only secret code (e.g., "40107" for 10%)
        public string DiscountCode { get; set; } = "";

        public DateTime ReceivedDate { get; set; } = DateTime.Now;
    }
}