using System.Collections.Generic;

namespace InventorySystem.Core.Entities
{
    public class Supplier
    {
        public int Id { get; set; }

        // Required Identifiers
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";

        // Optional Details (Initialized to "" to prevent null errors)
        public string Email { get; set; } = "";
        public string Address { get; set; } = "";

        // New "Extra" Column
        public string Note { get; set; } = "";

        // Relationship: One Supplier has many Invoices
        public List<PurchaseInvoice> Invoices { get; set; } = new();
    }
}