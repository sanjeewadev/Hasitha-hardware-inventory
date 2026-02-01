namespace InventorySystem.Core.Enums
{
    public enum UserRole
    {
        SuperAdmin = 0, // Hard-coded God mode
        Admin = 1,      // Can manage users and settings
        Employee = 2    // Restricted access (POS only)
    }
}