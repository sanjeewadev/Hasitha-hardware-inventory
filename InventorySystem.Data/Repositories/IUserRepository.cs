using InventorySystem.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventorySystem.Data.Repositories
{
    public interface IUserRepository
    {
        // Login & Security
        Task<User?> GetByUsernameAsync(string username);

        // Management
        Task<IEnumerable<User>> GetAllAsync();
        Task AddAsync(User user);
        Task UpdateAsync(User user);

        // We rarely hard-delete users to keep history safe, 
        // but we might need it for mistakes.
        Task DeleteAsync(int id);
    }
}