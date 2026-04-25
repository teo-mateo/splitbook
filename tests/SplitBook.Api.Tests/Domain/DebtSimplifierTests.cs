using FluentAssertions;
using SplitBook.Api.Domain;
using Xunit;

namespace SplitBook.Api.Tests.Domain;

public class DebtSimplifierTests
{
    [Fact]
    public void Simplify_TwoUsers_OppositeBalances_ReturnsSingleTransfer()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var balances = new List<(Guid UserId, long NetAmountMinor)>
        {
            (userA, 3000),
            (userB, -3000)
        };

        var result = DebtSimplifier.Simplify(balances);

        result.Should().HaveCount(1);
        var transfer = result.Single();
        transfer.FromUserId.Should().Be(userB);
        transfer.ToUserId.Should().Be(userA);
        transfer.AmountMinor.Should().Be(3000);
    }

    [Fact]
    public void Simplify_ThreeUsers_AtMostNMinus1Transfers()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var userC = Guid.NewGuid();

        var balances = new List<(Guid UserId, long NetAmountMinor)>
        {
            (userA, 5000),
            (userB, -3000),
            (userC, -2000)
        };

        var result = DebtSimplifier.Simplify(balances);

        var nonZeroCount = balances.Count(b => b.NetAmountMinor != 0);
        result.Count.Should().BeLessThanOrEqualTo(nonZeroCount - 1, "at most N-1 transfers for N non-zero members");
    }

    [Fact]
    public void Simplify_ClearsAllBalances()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var userC = Guid.NewGuid();
        var userD = Guid.NewGuid();

        var balances = new List<(Guid UserId, long NetAmountMinor)>
        {
            (userA, 7000),
            (userB, -2000),
            (userC, -3000),
            (userD, -2000)
        };

        var result = DebtSimplifier.Simplify(balances);

        // Simulate executing transfers
        var remaining = balances.ToDictionary(b => b.UserId, b => b.NetAmountMinor);

        foreach (var transfer in result)
        {
            remaining[transfer.FromUserId] += transfer.AmountMinor;
            remaining[transfer.ToUserId] -= transfer.AmountMinor;
        }

        foreach (var kvp in remaining)
        {
            kvp.Value.Should().Be(0, $"balance for user {kvp.Key} should be zeroed after executing all transfers");
        }
    }

    [Fact]
    public void Simplify_AllZeroBalances_ReturnsEmpty()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var balances = new List<(Guid UserId, long NetAmountMinor)>
        {
            (userA, 0),
            (userB, 0)
        };

        var result = DebtSimplifier.Simplify(balances);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Simplify_FourUsers_ComplexScenario_ClearsAllBalances()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var userC = Guid.NewGuid();
        var userD = Guid.NewGuid();

        // A=+4000, B=+1000, C=-3000, D=-2000
        // A plausible wrong implementation might pair A↔C and B↔D (2 transfers) but leave residuals
        // Correct greedy: A(-4000)↔C(+3000)→A left +1000, then A↔D 2000→A left -1000, B↔A 1000
        // Or: C→A 3000, D→A 1000, D→B 1000 (3 transfers = N-1)
        var balances = new List<(Guid UserId, long NetAmountMinor)>
        {
            (userA, 4000),
            (userB, 1000),
            (userC, -3000),
            (userD, -2000)
        };

        var result = DebtSimplifier.Simplify(balances);

        var nonZeroCount = balances.Count(b => b.NetAmountMinor != 0);
        result.Count.Should().BeLessThanOrEqualTo(nonZeroCount - 1);

        var remaining = balances.ToDictionary(b => b.UserId, b => b.NetAmountMinor);
        foreach (var transfer in result)
        {
            remaining[transfer.FromUserId] += transfer.AmountMinor;
            remaining[transfer.ToUserId] -= transfer.AmountMinor;
        }

        foreach (var kvp in remaining)
        {
            kvp.Value.Should().Be(0, $"balance for user {kvp.Key} should be zeroed");
        }
    }
}
