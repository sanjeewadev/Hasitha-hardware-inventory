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

        // --- FIX: Use decimal for financial math ---
        public decimal Discount { get; set; }        // Discount %

        // Owner-Only secret code
        public string DiscountCode { get; set; } = "";

        public DateTime ReceivedDate { get; set; } = DateTime.Now;
    }
}