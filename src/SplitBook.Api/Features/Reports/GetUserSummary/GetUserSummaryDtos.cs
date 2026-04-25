namespace SplitBook.Api.Features.Reports.GetUserSummary;

public record GroupSummaryDto(
    Guid GroupId,
    long NetAmountMinor,
    long GrossAmountMinor);

public record UserSummaryDto(
    List<GroupSummaryDto> Groups);
