using InventorySystem.Core.Enums;
using System;

namespace InventorySystem.Core.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;

        // SECURITY: We store the hash (e.g. "a5f3..."), NEVER the real password.
        public string PasswordHash { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty; // Optional: "John Doe"

        public UserRole Role { get; set; }

        public bool IsActive { get; set; } = true; // If false, user cannot log in.

        public string Permissions { get; set; } = ""; // Stores "Dashboard,POS,Stock"

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}