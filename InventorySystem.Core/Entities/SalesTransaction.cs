using InventorySystem.Core.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace InventorySystem.Core.Entities
{
    public enum PaymentStatus
    {
        Paid,           // Fully settled (Normal sales)
        Unpaid,         // 0 Paid (New Credit Sale)
        PartiallyPaid   // Some paid, some remaining
    }

    public class SalesTransaction
    {
        [Key]
        public string ReceiptId { get; set; } = string.Empty; // e.g. "202602021005"

        public DateTime TransactionDate { get; set; } = DateTime.Now;
        public decimal TotalAmount { get; set; }

        // Money Tracking
        public decimal PaidAmount { get; set; }

        // Calculated field (not stored in DB, but useful for logic)
        public decimal RemainingBalance => TotalAmount - PaidAmount;

        // Credit Info
        public bool IsCredit { get; set; } // True if "Credit Checkout" was used

        // This is where we store the name from your Popup
        public string? CustomerName { get; set; }

        public PaymentStatus Status { get; set; }
    }
}