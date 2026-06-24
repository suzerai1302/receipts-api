namespace Receipts.Core
{
    public record Settlement(string DebtorId, string CreditorId, decimal Amount);
}
