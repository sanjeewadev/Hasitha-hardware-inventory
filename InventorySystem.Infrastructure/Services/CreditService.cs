using InventorySystem.Core.Entities;
using InventorySystem.Data.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventorySystem.Infrastructure.Services
{
    public class CreditService
    {
        private readonly InventoryDbContext _context;

        public CreditService(InventoryDbContext context)
        {
            _context = context;
        }

        // 1. Get all people who owe money
        public async Task<List<SalesTransaction>> GetUnpaidTransactionsAsync()
        {
            return await _context.SalesTransactions
                .Where(t => t.Status != PaymentStatus.Paid) // Only Unpaid or Partial
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();
        }

        // 2. Add a Payment (The "Pay" Popup Logic)
        public async Task AddPaymentAsync(string receiptId, decimal amount, string note)
        {
            var transaction = await _context.SalesTransactions.FirstOrDefaultAsync(t => t.ReceiptId == receiptId);
            if (transaction == null) throw new Exception("Transaction not found.");

            // Validation
            if (amount <= 0) throw new Exception("Payment amount must be positive.");
            if (amount > (transaction.TotalAmount - transaction.PaidAmount)) throw new Exception("Amount exceeds remaining balance.");

            // A. Update the Master Record
            transaction.PaidAmount += amount;

            // B. Check if fully paid
            if (transaction.PaidAmount >= transaction.TotalAmount)
            {
                transaction.Status = PaymentStatus.Paid;
                transaction.PaidAmount = transaction.TotalAmount; // Safety clamp
            }
            else
            {
                transaction.Status = PaymentStatus.PartiallyPaid;
            }

            // C. Create the History Log
            var log = new CreditPaymentLog
            {
                ReceiptId = receiptId,
                AmountPaid = amount,
                PaymentDate = DateTime.Now,
                Note = note
            };

            _context.CreditPaymentLogs.Add(log);
            _context.SalesTransactions.Update(transaction);

            await _context.SaveChangesAsync();
        }

        // --- NEW: Get Items for a specific Receipt ---
        public async Task<List<StockMovement>> GetTransactionItemsAsync(string receiptId)
        {
            return await _context.StockMovements
                .Include(m => m.Product) // Include Product to get the Name
                .Where(m => m.ReceiptId == receiptId)
                .ToListAsync();
        }
    }
}