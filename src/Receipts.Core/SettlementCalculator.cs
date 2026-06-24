namespace Receipts.Core
{
    public static class SettlementCalculator
    {
        public static IReadOnlyList<Settlement> Calculate(IEnumerable<Expense> expenses)
        {
            var balances = new Dictionary<string, decimal>();
            foreach (var expense in expenses)
            {
                var share = expense.Amount / expense.ParticipantIds.Count;
                foreach (var participant in expense.ParticipantIds)
                    balances[participant] = balances.GetValueOrDefault(participant) - share;
                balances[expense.PayerId] = balances.GetValueOrDefault(expense.PayerId) + expense.Amount;
            }

            var debtors = balances.Where(b => b.Value < 0).Select(b => (Id: b.Key, Amount: -b.Value)).ToList();
            var creditors = balances.Where(b => b.Value > 0).Select(b => (Id: b.Key, Amount: b.Value)).ToList();
            var settlements = new List<Settlement>();
            int i = 0;
            int j = 0;
            while (i < debtors.Count && j < creditors.Count)
            {
                var pay = Math.Min(debtors[i].Amount, creditors[j].Amount);
                settlements.Add(new Settlement(debtors[i].Id, creditors[j].Id, pay));

                debtors[i] = (debtors[i].Id, debtors[i].Amount - pay);
                creditors[j] = (creditors[j].Id, creditors[j].Amount - pay);

                if (debtors[i].Amount == 0) i++;
                if (creditors[j].Amount == 0) j++;
            }

            return settlements;
        }
    }
}
