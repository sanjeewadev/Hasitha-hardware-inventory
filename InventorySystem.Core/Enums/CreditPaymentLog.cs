using System;

namespace InventorySystem.Core.Entities
{
    public class CreditPaymentLog
    {
        public int Id { get; set; }

        public string ReceiptId { get; set; } = string.Empty; // Links back to the Sale
        public SalesTransaction? SalesTransaction { get; set; }

        public decimal AmountPaid { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        public string? Note { get; set; } // "Paid by cash", "Wife came to pay", etc.
    }
}