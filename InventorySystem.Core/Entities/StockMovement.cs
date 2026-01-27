using InventorySystem.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventorySystem.Core.Entities
{
    public class StockMovement
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public StockMovementType Type { get; set; }

        public int Quantity { get; set; }

        public string? Note { get; set; }

        public DateTime Date { get; set; } = DateTime.UtcNow;
    }
}
