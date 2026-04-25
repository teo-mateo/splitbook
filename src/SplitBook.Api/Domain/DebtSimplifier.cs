namespace SplitBook.Api.Domain;

public static class DebtSimplifier
{
    /// <summary>
    /// Reduces a set of per-user balances to the minimum number of transfers
    /// that clears them all. Uses the greedy max-credit / max-debit pairing
    /// algorithm which guarantees at most N−1 transfers for N non-zero members.
    /// </summary>
    public static List<(Guid FromUserId, Guid ToUserId, long AmountMinor)> Simplify(
        List<(Guid UserId, long NetAmountMinor)> balances)
    {
        // Separate creditors (positive balance) and debtors (negative balance)
        var creditors = balances
            .Where(b => b.NetAmountMinor > 0)
            .OrderByDescending(b => b.NetAmountMinor)
            .ToList();

        var debtors = balances
            .Where(b => b.NetAmountMinor < 0)
            .OrderBy(b => b.NetAmountMinor)
            .ToList();

        var transfers = new List<(Guid FromUserId, Guid ToUserId, long AmountMinor)>();

        var debtorIdx = 0;
        var creditorIdx = 0;

        while (debtorIdx < debtors.Count && creditorIdx < creditors.Count)
        {
            var debtor = debtors[debtorIdx];
            var creditor = creditors[creditorIdx];

            // Transfer amount is the smaller of |debtor owes| and |creditor is owed|
            long amount = Math.Min(-debtor.NetAmountMinor, creditor.NetAmountMinor);

            transfers.Add((debtor.UserId, creditor.UserId, amount));

            // Adjust remaining balances
            var newDebtorBalance = debtor.NetAmountMinor + amount;
            var newCreditorBalance = creditor.NetAmountMinor - amount;

            if (newDebtorBalance == 0)
            {
                debtorIdx++;
            }
            else
            {
                debtors[debtorIdx] = (debtor.UserId, newDebtorBalance);
            }

            if (newCreditorBalance == 0)
            {
                creditorIdx++;
            }
            else
            {
                creditors[creditorIdx] = (creditor.UserId, newCreditorBalance);
            }
        }

        return transfers;
    }
}
