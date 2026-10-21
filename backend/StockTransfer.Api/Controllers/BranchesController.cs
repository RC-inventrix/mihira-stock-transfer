using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockTransfer.Api.Contracts;
using StockTransfer.Api.Data;
using StockTransfer.Api.Errors;

namespace StockTransfer.Api.Controllers;

[ApiController]
[Route("branches")]
public class BranchesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<BranchDto>> List(CancellationToken ct) =>
        await db.Branches
            .OrderBy(b => b.Id)
            .Select(b => new BranchDto(b.Id, b.Name, b.Type))
            .ToListAsync(ct);

    /// <summary>Stock level of every product at one branch, including products it holds none of.</summary>
    [HttpGet("{id:int}/stock")]
    public async Task<BranchStockDto> Stock(int id, CancellationToken ct)
    {
        var branchName = await db.Branches.Where(b => b.Id == id).Select(b => b.Name).SingleOrDefaultAsync(ct)
            ?? throw ApiException.NotFound($"Branch {id} does not exist.");

        var items = await db.Stock
            .Where(s => s.BranchId == id)
            .OrderBy(s => s.ProductId)
            .Select(s => new StockItemDto(s.ProductId, s.Product.Sku, s.Product.Name, s.Quantity))
            .ToListAsync(ct);

        return new BranchStockDto(id, branchName, items);
    }
}
