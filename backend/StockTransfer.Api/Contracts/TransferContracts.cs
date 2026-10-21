using StockTransfer.Api.Domain;

namespace StockTransfer.Api.Contracts;

public record CreateTransferLineRequest(int ProductId, int Quantity);

// Lines is nullable so a missing list reaches TransferService's validation and gets its message,
// rather than the framework's generic "field is required".
public record CreateTransferRequest(int FromBranchId, int ToBranchId, List<CreateTransferLineRequest>? Lines);

public record ReceiveTransferLineRequest(int ProductId, int ReceivedQuantity);

/// <summary>Optional body for receive. Lines left out (or no body at all) are received in full.</summary>
public record ReceiveTransferRequest(List<ReceiveTransferLineRequest>? Lines);

/// <param name="ReceivedQuantity">Null until the transfer is received. <c>Quantity - ReceivedQuantity</c> is the shortfall.</param>
public record TransferLineDto(int ProductId, string Sku, string ProductName, int Quantity, int? ReceivedQuantity);

public record TransferDto(
    int Id,
    int FromBranchId,
    string FromBranchName,
    int ToBranchId,
    string ToBranchName,
    TransferStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DispatchedAt,
    DateTimeOffset? ReceivedAt,
    DateTimeOffset? CancelledAt,
    IReadOnlyList<TransferLineDto> Lines);
