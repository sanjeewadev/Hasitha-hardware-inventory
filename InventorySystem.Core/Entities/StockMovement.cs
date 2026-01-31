using InventorySystem.Core.Enums;
using System;

namespace InventorySystem.Core.Entities
{
    public class StockMovement
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int Quantity { get; set; }
        public StockMovementType Type { get; set; } // In, Out, Adjustment
        public DateTime Date { get; set; } = DateTime.Now;
        public string Note { get; set; } = "";

        // --- FINANCIALS ---
        public decimal UnitCost { get; set; }   // What we BOUGHT it for (Profit Calc)
        public decimal UnitPrice { get; set; }  // What we SOLD it for (Revenue Calc) <--- ADD THIS

        // --- AUDIT ---
        public bool IsVoided { get; set; } = false;
    }
}