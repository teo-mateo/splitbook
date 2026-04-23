namespace SplitBook.Api.Domain;

public class Membership
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
}
