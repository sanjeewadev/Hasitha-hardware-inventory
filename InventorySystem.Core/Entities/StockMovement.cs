using InventorySystem.Core.Enums;
using System;

namespace InventorySystem.Core.Entities
{
    public class StockMovement
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        // --- UPDATED: Changed from int to decimal ---
        public decimal Quantity { get; set; }

        public StockMovementType Type { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public string Note { get; set; } = "";

        // Financials
        public decimal UnitCost { get; set; }
        public decimal UnitPrice { get; set; }

        // Tracking
        public string ReceiptId { get; set; } = "";
        public int? StockBatchId { get; set; }
        public StockBatch? StockBatch { get; set; }

        // Audit
        public AdjustmentReason Reason { get; set; }
        public int? UserId { get; set; }

        public bool IsVoided { get; set; } = false;
    }
}