namespace StockTransfer.Api.Domain;

/// <summary>
/// On-hand quantity of one product at one location. A row exists for every branch/product pair (seeded with
/// zero where there is none), so moving stock is always a plain update and never an insert-or-update race.
/// </summary>
public class Stock
{
    public int BranchId { get; set; }
    public int ProductId { get; set; }

    /// <summary>Never negative: enforced by a guarded UPDATE in code and a CHECK constraint in the database.</summary>
    public int Quantity { get; set; }

    public Branch Branch { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
