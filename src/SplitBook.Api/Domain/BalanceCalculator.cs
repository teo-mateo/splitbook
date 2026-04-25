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
        List<ExpenseSplit> splits)
    {
        var balanceByUser = new Dictionary<Guid, long>();
        foreach (var memberId in memberIds)
        {
            balanceByUser[memberId] = 0;
        }

        foreach (var expense in expenses)
        {
            var expenseSplits = splits.Where(s => s.ExpenseId == expense.Id).ToList();

            // The payer gets credit for the full expense amount
            if (balanceByUser.ContainsKey(expense.PayerUserId))
            {
                balanceByUser[expense.PayerUserId] += expense.AmountMinor;
            }

            // Each participant gets debited by their share
            foreach (var split in expenseSplits)
            {
                if (balanceByUser.ContainsKey(split.UserId))
                {
                    balanceByUser[split.UserId] -= split.AmountMinor;
                }
            }
        }

        return balanceByUser.Select(kvp => (kvp.Key, kvp.Value)).ToList();
    }
}
