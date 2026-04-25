namespace SplitBook.Api.Features.Settlements.RecordSettlement;

public record RecordSettlementRequest(
    Guid FromUserId,
    Guid ToUserId,
    long AmountMinor,
    string Currency,
    DateOnly OccurredOn
);

public record SettlementDto(
    Guid Id,
    Guid GroupId,
    Guid FromUserId,
    Guid ToUserId,
    long AmountMinor,
    string Currency,
    DateOnly OccurredOn,
    DateTimeOffset CreatedAt
);
