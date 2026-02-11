using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventorySystem.Core.Entities
{
    public class StockBatch
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public Product? Product { get; set; }

        public decimal InitialQuantity { get; set; }
        public decimal RemainingQuantity { get; set; }

        // Pricing
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal Discount { get; set; }

        public string DiscountCode { get; set; } = "";

        public DateTime ReceivedDate { get; set; } = DateTime.Now;

        // Link to the Bill
        public int? PurchaseInvoiceId { get; set; }
        public PurchaseInvoice? PurchaseInvoice { get; set; }

        public decimal MinSellingPrice
        {
            get
            {
                if (Discount <= 0) return SellingPrice;
                return SellingPrice - (SellingPrice * (Discount / 100m));
            }
        }

        // --- NEW: Live Math for UI Grid ---
        [NotMapped]
        public decimal TotalLineCost => InitialQuantity * CostPrice;
    }
}