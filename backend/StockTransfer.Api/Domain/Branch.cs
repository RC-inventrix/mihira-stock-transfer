namespace StockTransfer.Api.Domain;

public enum BranchType
{
    Warehouse,
    Branch,
}

/// <summary>
/// A stock-holding location. The warehouse is a location like any branch, so it can send and receive transfers.
/// </summary>
public class Branch
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public BranchType Type { get; set; }
}
