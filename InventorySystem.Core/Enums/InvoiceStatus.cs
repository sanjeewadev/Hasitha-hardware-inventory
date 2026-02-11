namespace InventorySystem.Core.Enums
{
    public enum InvoiceStatus
    {
        Draft = 0,   // Editable, NOT in POS
        Posted = 1   // Locked, Available in POS
    }
}