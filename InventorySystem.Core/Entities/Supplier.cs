using System.Collections.Generic;

namespace InventorySystem.Core.Entities
{
    public class Supplier
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";

        public string Note { get; set; } = "";

        // Relationship: One Supplier has many Invoices
        public List<PurchaseInvoice> Invoices { get; set; } = new();
    }
}