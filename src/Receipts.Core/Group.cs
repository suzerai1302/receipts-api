namespace Receipts.Core;

public class Group
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public List<string> MemberEmails { get; } = new();
    public List<Expense> Expenses { get; } = new();
}
