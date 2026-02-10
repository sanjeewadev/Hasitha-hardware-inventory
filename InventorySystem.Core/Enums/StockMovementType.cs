namespace InventorySystem.Core.Enums
{
    public enum StockMovementType
    {
        In = 1,
        Out = 2,
        Adjustment = 3,
        SalesReturn = 4,    // <--- NEW: Customer returned item (Stock UP)
        PurchaseReturn = 5  // <--- NEW: We returned item to Supplier (Stock DOWN)
    }
}