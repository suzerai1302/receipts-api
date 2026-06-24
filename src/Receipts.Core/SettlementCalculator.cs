namespace Receipts.Core
{
    public static class SettlementCalculator
    {
        public static IReadOnlyList<Settlement> Calculate(IEnumerable<Expense> expenses)
        {
            // PHASE 1 — compute each person's net balance (paid minus owed).
            //   - make a Dictionary<string, decimal> called balances
            //   - for each expense:
            //       share = Amount / ParticipantIds.Count
            //       for each participant: balances[participant] -= share   (they consumed a share)
            //       balances[payer]     += Amount                          (they fronted the whole bill)
            //   hint: use balances.GetValueOrDefault(key) to read a missing key as 0.
            //   end state: negative balance = owes money, positive = is owed money.
            var balances = new Dictionary<string, decimal>();
            foreach (var expense in expenses)
            {
                var share = expense.Amount / expense.ParticipantIds.Count;
                foreach (var participant in expense.ParticipantIds)
                    balances[participant] = balances.GetValueOrDefault(participant) - share;
                balances[expense.PayerId] = balances.GetValueOrDefault(expense.PayerId) + expense.Amount;
            }

            // PHASE 2 — split into debtors and creditors.
            //   - debtors   = people with balance < 0  (store their owed amount as a POSITIVE number)
            //   - creditors = people with balance > 0
            //   hint: LINQ .Where(...).Select(...).ToList(); a tuple (Id: ..., Amount: ...) is handy.
            var debtors = balances.Where(b => b.Value < 0).Select(b => (Id: b.Key, Amount: -b.Value)).ToList();
            var creditors = balances.Where(b => b.Value > 0).Select(b => (Id: b.Key, Amount: b.Value)).ToList();


            // PHASE 3 — greedily match debtors to creditors.
            //   - make the result List<Settlement>
            //   - walk both lists with two indices (i, j) while both have items:
            //       pay = the smaller of debtor[i].Amount and creditor[j].Amount   (Math.Min)
            //       add Settlement(debtor[i].Id, creditor[j].Id, pay)
            //       subtract pay from both
            //       advance i if its debtor hits 0; advance j if its creditor hits 0
            //   - return the result

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
