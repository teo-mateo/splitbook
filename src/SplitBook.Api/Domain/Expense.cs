namespace SplitBook.Api.Domain;

public class Expense
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid PayerUserId { get; set; }
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly OccurredOn { get; set; }
    public SplitMethod SplitMethod { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? IdempotencyKey { get; set; }
    public long Version { get; set; }
    public ICollection<ExpenseSplit> ExpenseSplits { get; set; } = new List<ExpenseSplit>();
}
