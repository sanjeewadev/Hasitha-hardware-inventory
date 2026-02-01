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
        public StockMovementType Type { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public string Note { get; set; } = "";

        // --- FINANCIALS ---
        public decimal UnitCost { get; set; }
        public decimal UnitPrice { get; set; }

        // --- TRACKING ---
        public string ReceiptId { get; set; } = "";
        public int? StockBatchId { get; set; }
        public StockBatch? StockBatch { get; set; }

        // --- AUDIT (Vulnerability 4 Fix) ---
        public AdjustmentReason Reason { get; set; } // <--- NEW
        public int? UserId { get; set; }             // <--- NEW (Who did it?)

        public bool IsVoided { get; set; } = false;
    }
}