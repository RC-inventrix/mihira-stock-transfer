namespace StockTransfer.Api.Domain;

/// <summary>
/// Allowed flows: Requested → Dispatched → Received, or Requested → Cancelled. Nothing else.
/// </summary>
public enum TransferStatus
{
    Requested,
    Dispatched,
    Received,
    Cancelled,
}

public class Transfer
{
    public int Id { get; set; }
    public int FromBranchId { get; set; }
    public int ToBranchId { get; set; }
    public TransferStatus Status { get; set; } = TransferStatus.Requested;

    /// <summary>
    /// Client-supplied key (unique when present) so a retried or double-clicked create returns the
    /// original transfer instead of creating a second one.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public Branch FromBranch { get; set; } = null!;
    public Branch ToBranch { get; set; } = null!;
    public List<TransferLine> Lines { get; set; } = [];
}

public class TransferLine
{
    public int Id { get; set; }
    public int TransferId { get; set; }
    public int ProductId { get; set; }
    /// <summary>The quantity sent. It leaves the sender on dispatch.</summary>
    public int Quantity { get; set; }

    /// <summary>
    /// What actually arrived, set on receive (null until then). It is between 0 and <see cref="Quantity"/>; the difference
    /// is the record of what was lost or damaged in transit.
    /// </summary>
    public int? ReceivedQuantity { get; set; }

    public Product Product { get; set; } = null!;
}
