using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockTransfer.Api.Contracts;
using StockTransfer.Api.Data;
using StockTransfer.Api.Domain;
using StockTransfer.Api.Errors;

namespace StockTransfer.Api.Services;

/// <summary>
/// The transfer rules. Each status change is one conditional UPDATE (<c>... WHERE "Status" = expected</c>) in the same
/// transaction as the stock changes it causes. The expected status is the concurrency token: PostgreSQL row-locks the
/// transfer, so a second dispatch or receive of the same transfer (double-click, retry, or a truly concurrent request)
/// waits for the first to commit, then matches no row and moves no stock.
/// </summary>
public class TransferService(AppDbContext db)
{
    private const int MaxIdempotencyKeyLength = 100;

    private static readonly Expression<Func<Transfer, TransferDto>> ToDto = t => new TransferDto(
        t.Id,
        t.FromBranchId,
        t.FromBranch.Name,
        t.ToBranchId,
        t.ToBranch.Name,
        t.Status,
        t.CreatedAt,
        t.DispatchedAt,
        t.ReceivedAt,
        t.CancelledAt,
        t.Lines
            .OrderBy(l => l.ProductId)
            .Select(l => new TransferLineDto(l.ProductId, l.Product.Sku, l.Product.Name, l.Quantity))
            .ToList());

    /// <param name="branchId">Matches transfers sent from or to this branch.</param>
    public async Task<IReadOnlyList<TransferDto>> ListAsync(int? branchId, TransferStatus? status, CancellationToken ct)
    {
        var query = db.Transfers.AsQueryable();
        if (branchId is { } branch)
        {
            query = query.Where(t => t.FromBranchId == branch || t.ToBranchId == branch);
        }
        if (status is { } wanted)
        {
            query = query.Where(t => t.Status == wanted);
        }

        return await query.OrderByDescending(t => t.Id).Select(ToDto).ToListAsync(ct);
    }

    public async Task<TransferDto> GetAsync(int id, CancellationToken ct) =>
        await db.Transfers.Where(t => t.Id == id).Select(ToDto).SingleOrDefaultAsync(ct)
        ?? throw ApiException.NotFound($"Transfer {id} does not exist.");

    /// <summary>Creates a Requested transfer. No stock moves until it is dispatched.</summary>
    /// <returns>The transfer, with <c>Created</c> false when the idempotency key matched an earlier request.</returns>
    public async Task<(TransferDto Transfer, bool Created)> CreateAsync(
        CreateTransferRequest request, string? idempotencyKey, CancellationToken ct)
    {
        idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (idempotencyKey is not null)
        {
            if (idempotencyKey.Length > MaxIdempotencyKeyLength)
            {
                throw ApiException.Invalid([$"The Idempotency-Key header must be at most {MaxIdempotencyKeyLength} characters."]);
            }
            if (await FindByKeyAsync(idempotencyKey, ct) is { } earlier)
            {
                return (earlier, false);
            }
        }

        var transfer = new Transfer
        {
            FromBranchId = request.FromBranchId,
            ToBranchId = request.ToBranchId,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTimeOffset.UtcNow,
            Lines = await ValidateLinesAsync(request, ct),
        };
        db.Transfers.Add(transfer);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (idempotencyKey is not null && e.InnerException is PostgresException
                                          {
                                              SqlState: PostgresErrorCodes.UniqueViolation,
                                              ConstraintName: "IX_Transfers_IdempotencyKey",
                                          })
        {
            // A concurrent request with the same key inserted first: answer with its transfer.
            db.ChangeTracker.Clear();
            return (await FindByKeyAsync(idempotencyKey, ct) ?? throw new InvalidOperationException("Key vanished."), false);
        }

        return (await GetAsync(transfer.Id, ct), true);
    }

    public Task<TransferDto> DispatchAsync(int id, CancellationToken ct) =>
        MoveAsync(id, TransferStatus.Requested, TransferStatus.Dispatched, t => t.DispatchedAt, TakeFromSenderAsync, ct);

    public Task<TransferDto> ReceiveAsync(int id, CancellationToken ct) =>
        MoveAsync(id, TransferStatus.Dispatched, TransferStatus.Received, t => t.ReceivedAt, AddToReceiverAsync, ct);

    public Task<TransferDto> CancelAsync(int id, CancellationToken ct) =>
        MoveAsync(id, TransferStatus.Requested, TransferStatus.Cancelled, t => t.CancelledAt, moveStock: null, ct);

    /// <summary>
    /// Moves a transfer from <paramref name="from"/> to <paramref name="to"/> and applies its stock change, all in one
    /// transaction. If <paramref name="moveStock"/> throws, the status change is rolled back with it.
    /// </summary>
    private async Task<TransferDto> MoveAsync(
        int id,
        TransferStatus from,
        TransferStatus to,
        Expression<Func<Transfer, DateTimeOffset?>> stampedAt,
        Func<Transfer, CancellationToken, Task>? moveStock,
        CancellationToken ct)
    {
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            DateTimeOffset? now = DateTimeOffset.UtcNow;
            var claimed = await db.Transfers
                .Where(t => t.Id == id && t.Status == from)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, to).SetProperty(stampedAt, now), ct);

            if (claimed == 1)
            {
                if (moveStock is not null)
                {
                    var transfer = await db.Transfers
                        .AsNoTracking()
                        .Include(t => t.Lines)
                        .Include(t => t.FromBranch)
                        .SingleAsync(t => t.Id == id, ct);
                    await moveStock(transfer, ct);
                }

                await tx.CommitAsync(ct);
                return await GetAsync(id, ct);
            }
        }

        // No row matched: the transfer does not exist, already took this step, or is in the wrong status.
        var current = await db.Transfers.Where(t => t.Id == id).Select(t => (TransferStatus?)t.Status).SingleOrDefaultAsync(ct)
            ?? throw ApiException.NotFound($"Transfer {id} does not exist.");

        // A repeat of a step that already happened (double-click, retry) gets the transfer back and changes nothing.
        var alreadyDone = current == to || (to == TransferStatus.Dispatched && current == TransferStatus.Received);
        if (alreadyDone)
        {
            return await GetAsync(id, ct);
        }

        throw ApiException.Conflict(
            "Action not allowed in this status",
            $"Transfer {id} is {current}. Only a {from} transfer can be {to.ToString().ToLowerInvariant()}.");
    }

    private async Task TakeFromSenderAsync(Transfer transfer, CancellationToken ct)
    {
        var shortLines = new List<TransferLine>();

        // Fixed product order, so two dispatches from the same branch lock its stock rows in the same order and cannot deadlock.
        foreach (var line in transfer.Lines.OrderBy(l => l.ProductId))
        {
            // Guarded decrement: it matches no row, and changes nothing, if the branch holds less than the line needs.
            var taken = await db.Stock
                .Where(s => s.BranchId == transfer.FromBranchId && s.ProductId == line.ProductId && s.Quantity >= line.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, x => x.Quantity - line.Quantity), ct);
            if (taken == 0)
            {
                shortLines.Add(line);
            }
        }

        if (shortLines.Count == 0)
        {
            return;
        }

        var shortIds = shortLines.Select(l => l.ProductId).ToList();
        var available = await db.Stock
            .Where(s => s.BranchId == transfer.FromBranchId && shortIds.Contains(s.ProductId))
            .Select(s => new { s.ProductId, s.Product.Name, s.Quantity })
            .ToDictionaryAsync(s => s.ProductId, ct);
        var messages = shortLines
            .Select(l => $"{available[l.ProductId].Name}: {l.Quantity} needed, {available[l.ProductId].Quantity} available.")
            .ToList();

        throw ApiException.Conflict(
            "Not enough stock",
            $"{transfer.FromBranch.Name} does not have enough stock to dispatch transfer {transfer.Id}. {string.Join(" ", messages)}",
            messages);
    }

    private async Task AddToReceiverAsync(Transfer transfer, CancellationToken ct)
    {
        foreach (var line in transfer.Lines.OrderBy(l => l.ProductId))
        {
            var added = await db.Stock
                .Where(s => s.BranchId == transfer.ToBranchId && s.ProductId == line.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, x => x.Quantity + line.Quantity), ct);

            // Every branch/product pair is seeded with a stock row, so this only fails if that invariant is broken.
            if (added != 1)
            {
                throw new InvalidOperationException(
                    $"No stock row for branch {transfer.ToBranchId}, product {line.ProductId}.");
            }
        }
    }

    private async Task<TransferDto?> FindByKeyAsync(string idempotencyKey, CancellationToken ct) =>
        await db.Transfers.Where(t => t.IdempotencyKey == idempotencyKey).Select(ToDto).SingleOrDefaultAsync(ct);

    /// <summary>Checks the request against the transfer rules and reports every problem at once.</summary>
    private async Task<List<TransferLine>> ValidateLinesAsync(CreateTransferRequest request, CancellationToken ct)
    {
        var errors = new List<string>();

        if (request.FromBranchId == request.ToBranchId)
        {
            errors.Add("The from and to branches must be different.");
        }

        var branchIds = await db.Branches
            .Where(b => b.Id == request.FromBranchId || b.Id == request.ToBranchId)
            .Select(b => b.Id)
            .ToListAsync(ct);
        if (!branchIds.Contains(request.FromBranchId))
        {
            errors.Add($"From branch {request.FromBranchId} does not exist.");
        }
        if (!branchIds.Contains(request.ToBranchId))
        {
            errors.Add($"To branch {request.ToBranchId} does not exist.");
        }

        var lines = request.Lines ?? [];
        if (lines.Count == 0)
        {
            errors.Add("A transfer needs at least one product line.");
        }

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var productNames = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        string NameOf(int productId) => productNames.GetValueOrDefault(productId, $"Product {productId}");

        errors.AddRange(productIds
            .Where(id => !productNames.ContainsKey(id))
            .Select(id => $"Product {id} does not exist."));
        errors.AddRange(lines
            .GroupBy(l => l.ProductId)
            .Where(g => g.Count() > 1)
            .Select(g => $"{NameOf(g.Key)} is on more than one line. Put each product on one line."));
        errors.AddRange(lines
            .Where(l => l.Quantity <= 0)
            .Select(l => $"The quantity of {NameOf(l.ProductId)} must be greater than zero."));

        if (errors.Count > 0)
        {
            throw ApiException.Invalid(errors);
        }

        return lines.Select(l => new TransferLine { ProductId = l.ProductId, Quantity = l.Quantity }).ToList();
    }
}
