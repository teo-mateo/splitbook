namespace SplitBook.Api.Features.Groups.CreateGroup;

public record CreateGroupRequest(string Name, string Currency);

public record CreateGroupResponse(Guid Id, string Name, string Currency, DateTimeOffset CreatedAt);
