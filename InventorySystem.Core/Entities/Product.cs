using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Core.Entities
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;
        public string? Description { get; set; }

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        // Pricing
        public decimal BuyingPrice { get; set; }
        public decimal SellingPrice { get; set; }

        // --- FIX: Use decimal for financial math ---
        public decimal DiscountLimit { get; set; }

        public int Quantity { get; set; }
        public int LowStockThreshold { get; set; } = 5;

        // --- SOFT DELETE FLAG ---
        public bool IsActive { get; set; } = true;

        public ICollection<StockBatch> Batches { get; set; } = new List<StockBatch>();
    }
}