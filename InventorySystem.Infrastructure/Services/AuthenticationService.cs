using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using InventorySystem.Data.Repositories;
using System;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace InventorySystem.Infrastructure.Services
{
    public class AuthenticationService
    {
        private readonly IUserRepository _userRepository;

        public AuthenticationService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<User?> LoginAsync(string username, string password)
        {
            // --- 1. PERMANENT SUPER ADMIN (The "Root" Account) ---
            // This account exists ONLY in code. It cannot be blocked or edited.
            // CHANGE THIS PASSWORD TO SOMETHING ONLY YOU KNOW!
            if (username == "1" && password == "1")
            {
                return new User
                {
                    Id = -1, // Negative ID to indicate it's not in the DB
                    Username = "master_admin",
                    Role = UserRole.SuperAdmin,
                    FullName = "SYSTEM ROOT",
                    IsActive = true
                };
            }
            // -----------------------------------------------------

            // 2. Fetch User from Database
            var user = await _userRepository.GetByUsernameAsync(username);

            // 3. Security: Generic "Not Found" response
            if (user == null) return null;

            // 4. Verify Password
            string inputHash = HashPassword(password);
            if (user.PasswordHash != inputHash)
            {
                return null;
            }

            // 5. Security: Check if Account is Blocked
            if (!user.IsActive)
            {
                throw new AuthenticationException("Account Disabled");
            }

            return user;
        }

        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;

            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
    }
}