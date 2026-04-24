namespace SplitBook.Api.Domain;

public class ExpenseSplit
{
    public Guid ExpenseId { get; set; }
    public Guid UserId { get; set; }
    public long AmountMinor { get; set; }
    public double? Percentage { get; set; }
    public int? Shares { get; set; }
}
