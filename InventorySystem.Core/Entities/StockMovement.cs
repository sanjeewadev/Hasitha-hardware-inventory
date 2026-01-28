using InventorySystem.Core.Enums;
using System;

namespace InventorySystem.Core.Entities
{
    public class StockMovement
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public StockMovementType Type { get; set; }
        public int Quantity { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string? Note { get; set; } // Stores "Sold @ 150" or "Void Reason: Mistake"

        // --- NEW: Audit Flag ---
        public bool IsVoided { get; set; } = false;

        // --- NEW FINANCIAL COLUMNS ---
        public decimal UnitCost { get; set; }   // How much you BOUGHT it for
        public decimal UnitPrice { get; set; }  // How much you SOLD it for
    }
}