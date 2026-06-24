namespace Receipts.Core
{
    public record Expense(string PayerId, decimal Amount, IReadOnlyList<string> ParticipantIds);
}
