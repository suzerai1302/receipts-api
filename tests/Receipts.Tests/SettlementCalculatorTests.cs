using Receipts.Core;

namespace Receipts.Tests;

public class SettlementCalculatorTests
{
    [Fact]
    public void OneExpense_SplitEqually_ParticipantsOwePayerTheirShare()
    {
        // A paid 90 for a dinner shared equally by A, B, C  ->  30 each.
        // A covered 90 but only owes a 30 share, so A is owed 60.
        // B and C each owe A their 30 share.
        var expenses = new[]
        {
            new Expense(PayerId: "A", Amount: 90m, ParticipantIds: new[] { "A", "B", "C" })
        };

        var settlements = SettlementCalculator.Calculate(expenses);

        Assert.Equal(2, settlements.Count);
        Assert.Contains(settlements, s => s.DebtorId == "B" && s.CreditorId == "A" && s.Amount == 30m);
        Assert.Contains(settlements, s => s.DebtorId == "C" && s.CreditorId == "A" && s.Amount == 30m);
    }

    [Fact]
    public void CrossPaidExpenses_NetOutToSingleDebt()
    {
        // B paid 200 shared by A,B  -> A owes B 100.
        // A paid 60  shared by A,B  -> B owes A 30.
        // Net: A owes B 70 (one settlement, not two opposing ones).
        var expenses = new[]
        {
            new Expense(PayerId: "B", Amount: 200m, ParticipantIds: new[] { "A", "B" }),
            new Expense(PayerId: "A", Amount: 60m,  ParticipantIds: new[] { "A", "B" })
        };

        var settlements = SettlementCalculator.Calculate(expenses);

        var single = Assert.Single(settlements);
        Assert.Equal("A", single.DebtorId);
        Assert.Equal("B", single.CreditorId);
        Assert.Equal(70m, single.Amount);
    }

    [Fact]
    public void ChainedDebts_AreSimplified_SoMiddlePersonDropsOut()
    {
        // B paid 100 shared by A,B  -> A owes B 50.
        // C paid 100 shared by B,C  -> B owes C 50.
        // Naive = 2 hops (A->B, B->C). Net: B is even, so A->C 50 directly. 1 transaction.
        var expenses = new[]
        {
          new Expense("B", 100m, new[] { "A", "B" }),
          new Expense("C", 100m, new[] { "B", "C" })
      };

        var settlements = SettlementCalculator.Calculate(expenses);

        var single = Assert.Single(settlements);
        Assert.Equal("A", single.DebtorId);
        Assert.Equal("C", single.CreditorId);
        Assert.Equal(50m, single.Amount);
    }

    [Fact]
    public void NoExpenses_ProducesNoSettlements()
    {
        var settlements = SettlementCalculator.Calculate(Array.Empty<Expense>());

        Assert.Empty(settlements);
    }
}
