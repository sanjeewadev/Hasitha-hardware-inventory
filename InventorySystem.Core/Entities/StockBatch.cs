using System;

namespace InventorySystem.Core.Entities
{
    public class StockBatch
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public decimal InitialQuantity { get; set; }
        public decimal RemainingQuantity { get; set; }

        // Pricing
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal Discount { get; set; }

        public string DiscountCode { get; set; } = "";

        public DateTime ReceivedDate { get; set; } = DateTime.Now;

        // --- NEW: Calculated Property ---
        public decimal MinSellingPrice
        {
            get
            {
                if (Discount <= 0) return SellingPrice;
                return SellingPrice - (SellingPrice * (Discount / 100m));
            }
        }
    }
}