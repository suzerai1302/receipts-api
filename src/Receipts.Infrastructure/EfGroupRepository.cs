using Microsoft.EntityFrameworkCore;
using Receipts.Core;

namespace Receipts.Infrastructure;

public class EfGroupRepository(ReceiptsDbContext db) : IGroupRepository
{
    public Task<Group?> GetByIdAsync(Guid id) =>
        db.Groups.FirstOrDefaultAsync(g => g.Id == id);

    public async Task AddAsync(Group group)
    {
        db.Groups.Add(group);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Group group)
    {
        db.Groups.Update(group);
        await db.SaveChangesAsync();
    }
}
