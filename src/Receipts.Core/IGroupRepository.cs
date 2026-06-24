namespace Receipts.Core;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(Guid id);
    Task AddAsync(Group group);
    Task UpdateAsync(Group group);
}
