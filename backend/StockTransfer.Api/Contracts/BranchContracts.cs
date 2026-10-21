using StockTransfer.Api.Domain;

namespace StockTransfer.Api.Contracts;

public record BranchDto(int Id, string Name, BranchType Type);

public record StockItemDto(int ProductId, string Sku, string ProductName, int Quantity);

public record BranchStockDto(int BranchId, string BranchName, IReadOnlyList<StockItemDto> Items);
