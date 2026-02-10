using System;

namespace InventorySystem.Core.Entities
{
    public class StockBatch
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        // FIX: Make this Nullable (?) to prevent "Cannot convert null" error
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
    }
}