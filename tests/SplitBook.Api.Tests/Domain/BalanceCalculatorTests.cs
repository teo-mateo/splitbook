using FluentAssertions;
using SplitBook.Api.Domain;
using Xunit;

namespace SplitBook.Api.Tests.Domain;

public class BalanceCalculatorTests
{
    [Fact]
    public void BalanceCalculator_SingleExpense_ComputesCorrectBalances()
    {
        // Arrange
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var expenseId = Guid.NewGuid();

        var expense = new Expense
        {
            Id = expenseId,
            PayerUserId = userA,
            AmountMinor = 6000,
            Currency = "EUR"
        };

        var splits = new List<ExpenseSplit>
        {
            new() { ExpenseId = expenseId, UserId = userA, AmountMinor = 3000 },
            new() { ExpenseId = expenseId, UserId = userB, AmountMinor = 3000 }
        };

        var memberIds = new List<Guid> { userA, userB };
        var expenses = new List<Expense> { expense };

        // Act
        var balances = BalanceCalculator.Calculate(memberIds, expenses, splits, new List<Settlement>());

        // Assert
        var userABalance = balances.Single(b => b.UserId == userA);
        var userBBalance = balances.Single(b => b.UserId == userB);

        // userA paid 6000, owes 3000 → net +3000
        userABalance.NetAmountMinor.Should().Be(3000);

        // userB paid 0, owes 3000 → net -3000
        userBBalance.NetAmountMinor.Should().Be(-3000);
    }

    [Fact]
    public void BalanceCalculator_MultipleExpenses_BalancesSumToZero()
    {
        // Arrange
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var userC = Guid.NewGuid();

        var expense1Id = Guid.NewGuid();
        var expense2Id = Guid.NewGuid();

        // Expense 1: 10000, paid by userA, splits: A=4000, B=3000, C=3000
        var expense1 = new Expense
        {
            Id = expense1Id,
            PayerUserId = userA,
            AmountMinor = 10000,
            Currency = "EUR"
        };

        // Expense 2: 6000, paid by userB, splits: A=2000, B=2000, C=2000
        var expense2 = new Expense
        {
            Id = expense2Id,
            PayerUserId = userB,
            AmountMinor = 6000,
            Currency = "EUR"
        };

        var expenses = new List<Expense> { expense1, expense2 };

        var splits = new List<ExpenseSplit>
        {
            new() { ExpenseId = expense1Id, UserId = userA, AmountMinor = 4000 },
            new() { ExpenseId = expense1Id, UserId = userB, AmountMinor = 3000 },
            new() { ExpenseId = expense1Id, UserId = userC, AmountMinor = 3000 },
            new() { ExpenseId = expense2Id, UserId = userA, AmountMinor = 2000 },
            new() { ExpenseId = expense2Id, UserId = userB, AmountMinor = 2000 },
            new() { ExpenseId = expense2Id, UserId = userC, AmountMinor = 2000 },
        };

        var memberIds = new List<Guid> { userA, userB, userC };

        // Act
        var balances = BalanceCalculator.Calculate(memberIds, expenses, splits, new List<Settlement>());

        // Assert — invariant: balances must sum to zero
        balances.Sum(b => b.NetAmountMinor).Should().Be(0L);
    }

    [Fact]
    public void BalanceCalculator_Settlement_AdjustsBalancesCorrectly()
    {
        // Arrange
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var expenseId = Guid.NewGuid();

        // Expense: 6000, paid by A, equal split → A: +3000, B: -3000
        var expense = new Expense
        {
            Id = expenseId,
            PayerUserId = userA,
            AmountMinor = 6000,
            Currency = "EUR"
        };

        var splits = new List<ExpenseSplit>
        {
            new() { ExpenseId = expenseId, UserId = userA, AmountMinor = 3000 },
            new() { ExpenseId = expenseId, UserId = userB, AmountMinor = 3000 }
        };

        // Settlement: B pays A 3000 → should zero both balances
        var settlement = new Settlement
        {
            Id = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            FromUserId = userB,
            ToUserId = userA,
            AmountMinor = 3000,
            Currency = "EUR"
        };

        var memberIds = new List<Guid> { userA, userB };
        var expenses = new List<Expense> { expense };
        var settlements = new List<Settlement> { settlement };

        // Act
        var balances = BalanceCalculator.Calculate(memberIds, expenses, splits, settlements);

        // Assert — both balances should be zero after settlement
        var userABalance = balances.Single(b => b.UserId == userA);
        var userBBalance = balances.Single(b => b.UserId == userB);

        // A: +3000 (from expense) - 3000 (from settlement, received) = 0
        userABalance.NetAmountMinor.Should().Be(0, "user A received 3000 from B, clearing their positive balance");

        // B: -3000 (from expense) + 3000 (from settlement, paid) = 0
        userBBalance.NetAmountMinor.Should().Be(0, "user B paid 3000 to A, clearing their negative balance");
    }
}
