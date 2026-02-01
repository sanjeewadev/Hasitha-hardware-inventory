namespace InventorySystem.Core.Enums
{
    public enum AdjustmentReason
    {
        Correction, // Use for counting errors (No financial impact on reports)
        Lost        // Use for Damaged/Stolen items (Recorded as a Loss)
    }
}