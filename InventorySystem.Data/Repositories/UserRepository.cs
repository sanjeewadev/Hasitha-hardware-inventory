using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums; // Needed for Role check
using InventorySystem.Data.Context;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq; // Needed for Where
using System.Threading.Tasks;

namespace InventorySystem.Data.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly InventoryDbContext _context;

        public UserRepository(InventoryDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            // SECURITY: Hide any SuperAdmin from the UI list.
            // This prevents regular Admins from editing/blocking the SuperAdmin.
            return await _context.Users
                .Where(u => u.Role != UserRole.SuperAdmin)
                .ToListAsync();
        }

        public async Task AddAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
        }
    }
}