using System;
using System.Collections.Generic;

namespace InventorySystem.Core.Entities
{
    public class PurchaseInvoice
    {
        public int Id { get; set; }

        public string BillNumber { get; set; } = ""; // e.g. "INV-9988"
        public DateTime Date { get; set; } = DateTime.Now;
        public decimal TotalAmount { get; set; }
        public string Note { get; set; } = "";

        // Link to Supplier
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        // Relationship: One Invoice contains many Batches of items
        public List<StockBatch> Batches { get; set; } = new();
    }
}