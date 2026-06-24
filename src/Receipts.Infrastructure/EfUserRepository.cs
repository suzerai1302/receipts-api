using Microsoft.EntityFrameworkCore;
using Receipts.Core;

namespace Receipts.Infrastructure;

public class EfUserRepository(ReceiptsDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task AddAsync(User user)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}
