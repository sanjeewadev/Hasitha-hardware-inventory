using InventorySystem.Core.Enums; // <--- Make sure this is here

namespace InventorySystem.Core.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public UserRole Role { get; set; } // Uses your Admin/Employee enum
    }
}