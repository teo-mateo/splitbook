namespace SplitBook.Api.Features.Groups.ListMyGroups;

public record GroupDto(Guid Id, string Name, string Currency, DateTimeOffset CreatedAt);
