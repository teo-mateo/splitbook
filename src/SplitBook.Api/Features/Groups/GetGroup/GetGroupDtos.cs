namespace SplitBook.Api.Features.Groups.GetGroup;

public record GroupDetailDto(
    Guid Id,
    string Name,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt,
    List<MemberDto> Members);

public record MemberDto(
    Guid UserId,
    string DisplayName);
