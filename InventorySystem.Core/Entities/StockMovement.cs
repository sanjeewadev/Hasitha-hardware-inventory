using InventorySystem.Core.Enums;
using System;

namespace InventorySystem.Core.Entities
{
    public class StockMovement
    {
        public int Id { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public StockMovementType Type { get; set; }
        public decimal Quantity { get; set; }

        // Pricing Snapshots
        public decimal UnitCost { get; set; }
        public decimal UnitPrice { get; set; }

        // --- PROPERTIES REQUIRED BY REPOSITORY ---
        public int? StockBatchId { get; set; }
        public StockBatch? StockBatch { get; set; }

        // Logic: 0 = Correction, 1 = Lost/Damaged
        public AdjustmentReason Reason { get; set; }

        public bool IsVoided { get; set; }

        // FIX: Renamed 'Description' to 'Note' to match StockInViewModel
        public string Note { get; set; } = "";

        public string ReceiptId { get; set; } = "";

        public decimal LineTotal => Quantity * UnitPrice;
    }
}