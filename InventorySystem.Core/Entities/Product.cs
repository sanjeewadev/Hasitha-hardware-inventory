using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventorySystem.Core.Entities
{
    public class Product
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        public decimal BuyingPrice { get; set; }
        public decimal SellingPrice { get; set; }

        public int Quantity { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
