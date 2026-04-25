namespace SplitBook.Api.Features.Balances.GetGroupBalances;

public record BalanceDto(
    Guid UserId,
    long NetAmountMinor);
