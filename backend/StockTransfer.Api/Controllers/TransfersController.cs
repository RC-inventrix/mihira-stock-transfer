using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using StockTransfer.Api.Contracts;
using StockTransfer.Api.Domain;
using StockTransfer.Api.Services;

namespace StockTransfer.Api.Controllers;

/// <summary>
/// Dispatch, receive and cancel are safe to repeat: a second call for a step that already happened returns 200 with the
/// transfer and moves nothing. A call from the wrong status returns 409 with the reason.
/// </summary>
[ApiController]
[Route("transfers")]
public class TransfersController(TransferService transfers) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TransferDto>> List(int? branchId, TransferStatus? status, CancellationToken ct) =>
        transfers.ListAsync(branchId, status, ct);

    [HttpGet("{id:int}")]
    public Task<TransferDto> Get(int id, CancellationToken ct) => transfers.GetAsync(id, ct);

    /// <summary>
    /// 201 for a new transfer. A retry with the same <c>Idempotency-Key</c> header returns 200 with the original.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TransferDto>> Create(
        CreateTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var (transfer, created) = await transfers.CreateAsync(request, idempotencyKey, ct);
        return created ? CreatedAtAction(nameof(Get), new { id = transfer.Id }, transfer) : Ok(transfer);
    }

    [HttpPost("{id:int}/dispatch")]
    public Task<TransferDto> Dispatch(int id, CancellationToken ct) => transfers.DispatchAsync(id, ct);

    /// <summary>
    /// The body is optional: send <c>{ "lines": [{ "productId", "receivedQuantity" }] }</c> to record that less arrived than
    /// was sent. Lines left out are received in full.
    /// </summary>
    [HttpPost("{id:int}/receive")]
    public Task<TransferDto> Receive(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReceiveTransferRequest? request,
        CancellationToken ct) =>
        transfers.ReceiveAsync(id, request, ct);

    [HttpPost("{id:int}/cancel")]
    public Task<TransferDto> Cancel(int id, CancellationToken ct) => transfers.CancelAsync(id, ct);
}
