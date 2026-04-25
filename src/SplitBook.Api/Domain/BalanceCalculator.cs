namespace SplitBook.Api.Domain;

public static class BalanceCalculator
{
    /// <summary>
    /// Computes net balance per member: sum(paid) - sum(owed share).
    /// Positive = others owe this member. Negative = this member owes others.
    /// </summary>
    public static List<(Guid UserId, long NetAmountMinor)> Calculate(
        List<Guid> memberIds,
        List<Expense> expenses,
        List<ExpenseSplit> splits,
        List<Settlement> settlements)
    {
        var balanceByUser = new Dictionary<Guid, long>();
        foreach (var memberId in memberIds)
        {
            balanceByUser[memberId] = 0;
        }

        // Expenses: payer gets credit for full amount, each participant gets debited by their share
        foreach (var expense in expenses)
        {
            var expenseSplits = splits.Where(s => s.ExpenseId == expense.Id).ToList();

            if (balanceByUser.ContainsKey(expense.PayerUserId))
            {
                balanceByUser[expense.PayerUserId] += expense.AmountMinor;
            }

            foreach (var split in expenseSplits)
            {
                if (balanceByUser.ContainsKey(split.UserId))
                {
                    balanceByUser[split.UserId] -= split.AmountMinor;
                }
            }
        }

        // Settlements: fromUserId pays toUserId — fromUserId balance increases (they paid), toUserId balance decreases (they received)
        foreach (var settlement in settlements)
        {
            if (balanceByUser.ContainsKey(settlement.FromUserId))
            {
                balanceByUser[settlement.FromUserId] += settlement.AmountMinor;
            }

            if (balanceByUser.ContainsKey(settlement.ToUserId))
            {
                balanceByUser[settlement.ToUserId] -= settlement.AmountMinor;
            }
        }

        return balanceByUser.Select(kvp => (kvp.Key, kvp.Value)).ToList();
    }
}
