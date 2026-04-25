namespace SplitBook.Api.Features.Balances.GetSimplifiedDebts;

public record SimplifiedDebtDto(
    Guid FromUserId,
    Guid ToUserId,
    long AmountMinor);
