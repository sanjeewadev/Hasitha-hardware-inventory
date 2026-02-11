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

        public string Unit { get; set; } = "Pcs";

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        // Pricing
        public decimal BuyingPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal DiscountLimit { get; set; }

        public decimal Quantity { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<StockBatch> Batches { get; set; } = new List<StockBatch>();
    }
}