using System.Net;
using StockTransfer.Api.Domain;
using StockTransfer.Tests.Infrastructure;
using static StockTransfer.Api.Data.SeedData;

namespace StockTransfer.Tests;

[Collection(ApiCollection.Name)]
public class TransferWorkflowTests(ApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task Creating_a_transfer_moves_no_stock()
    {
        var transfer = await Api.CreateAsync(WarehouseId, KandyId, (RiceId, 10));

        Assert.Equal(TransferStatus.Requested, transfer.Status);
        Assert.Equal(500, await Api.StockAsync(WarehouseId, RiceId));
        Assert.Equal(5, await Api.StockAsync(KandyId, RiceId));
    }

    [Fact]
    public async Task Dispatch_takes_stock_from_the_sender_and_receive_adds_it_to_the_receiver()
    {
        var transfer = await Api.CreateAsync(WarehouseId, KandyId, (RiceId, 10), (TeaId, 5));

        Assert.Equal(HttpStatusCode.OK, (await Api.DispatchAsync(transfer.Id)).StatusCode);
        Assert.Equal(TransferStatus.Dispatched, await Api.StatusAsync(transfer.Id));
        Assert.Equal(490, await Api.StockAsync(WarehouseId, RiceId));
        Assert.Equal(245, await Api.StockAsync(WarehouseId, TeaId));
        // In transit: the receiver has nothing yet.
        Assert.Equal(5, await Api.StockAsync(KandyId, RiceId));
        Assert.Equal(0, await Api.StockAsync(KandyId, TeaId));

        Assert.Equal(HttpStatusCode.OK, (await Api.ReceiveAsync(transfer.Id)).StatusCode);
        Assert.Equal(TransferStatus.Received, await Api.StatusAsync(transfer.Id));
        Assert.Equal(15, await Api.StockAsync(KandyId, RiceId));
        Assert.Equal(5, await Api.StockAsync(KandyId, TeaId));
        Assert.Equal(490, await Api.StockAsync(WarehouseId, RiceId));
    }

    [Fact]
    public async Task Dispatch_that_would_take_stock_below_zero_is_rejected_and_moves_nothing()
    {
        // Galle has 15 rice and 3 coconut oil. The rice line could be met on its own; the oil line cannot.
        var transfer = await Api.CreateAsync(GalleId, ColomboId, (RiceId, 2), (CoconutOilId, 4));

        var response = await Api.DispatchAsync(transfer.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await TransferApi.ReadAsync<Problem>(response);
        Assert.Equal("Not enough stock", problem.Title);
        Assert.Equal(["Coconut Oil 1L: 4 needed, 3 available."], problem.Messages);

        // All or nothing: the rice already taken in the same transaction was rolled back.
        Assert.Equal(15, await Api.StockAsync(GalleId, RiceId));
        Assert.Equal(3, await Api.StockAsync(GalleId, CoconutOilId));
        Assert.Equal(TransferStatus.Requested, await Api.StatusAsync(transfer.Id));
    }

    [Fact]
    public async Task Dispatching_twice_moves_stock_once()
    {
        var transfer = await Api.CreateAsync(WarehouseId, KandyId, (RiceId, 10));

        var first = await Api.DispatchAsync(transfer.Id);
        var second = await Api.DispatchAsync(transfer.Id);

        // The repeat is answered as a success (it is what the user wanted) but changes nothing.
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(490, await Api.StockAsync(WarehouseId, RiceId));
    }

    [Fact]
    public async Task Receiving_twice_adds_stock_once()
    {
        var transfer = await Api.CreateAsync(WarehouseId, KandyId, (RiceId, 10));
        await Api.DispatchAsync(transfer.Id);

        var first = await Api.ReceiveAsync(transfer.Id);
        var second = await Api.ReceiveAsync(transfer.Id);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(15, await Api.StockAsync(KandyId, RiceId));
    }

    [Fact]
    public async Task Cancelling_a_requested_transfer_moves_no_stock()
    {
        var transfer = await Api.CreateAsync(WarehouseId, KandyId, (RiceId, 10));

        Assert.Equal(HttpStatusCode.OK, (await Api.CancelAsync(transfer.Id)).StatusCode);

        Assert.Equal(TransferStatus.Cancelled, await Api.StatusAsync(transfer.Id));
        Assert.Equal(500, await Api.StockAsync(WarehouseId, RiceId));
        Assert.Equal(5, await Api.StockAsync(KandyId, RiceId));
    }

    [Theory]
    [InlineData(new string[0], "receive", "Transfer {0} is Requested. Only a Dispatched transfer can be received.")]
    [InlineData(new[] { "dispatch" }, "cancel", "Transfer {0} is Dispatched. Only a Requested transfer can be cancelled.")]
    [InlineData(new[] { "dispatch", "receive" }, "cancel", "Transfer {0} is Received. Only a Requested transfer can be cancelled.")]
    [InlineData(new[] { "cancel" }, "dispatch", "Transfer {0} is Cancelled. Only a Requested transfer can be dispatched.")]
    [InlineData(new[] { "cancel" }, "receive", "Transfer {0} is Cancelled. Only a Dispatched transfer can be received.")]
    public async Task An_action_from_the_wrong_status_is_rejected_and_moves_no_stock(
        string[] earlierSteps, string action, string expectedMessage)
    {
        var transfer = await Api.CreateAsync(WarehouseId, KandyId, (RiceId, 10));
        foreach (var step in earlierSteps)
        {
            Assert.Equal(HttpStatusCode.OK, (await Act(step, transfer.Id)).StatusCode);
        }
        var statusBefore = await Api.StatusAsync(transfer.Id);
        var senderBefore = await Api.StockAsync(WarehouseId, RiceId);
        var receiverBefore = await Api.StockAsync(KandyId, RiceId);

        var response = await Act(action, transfer.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(string.Format(expectedMessage, transfer.Id), (await TransferApi.ReadAsync<Problem>(response)).Detail);
        Assert.Equal(statusBefore, await Api.StatusAsync(transfer.Id));
        Assert.Equal(senderBefore, await Api.StockAsync(WarehouseId, RiceId));
        Assert.Equal(receiverBefore, await Api.StockAsync(KandyId, RiceId));
    }

    [Fact]
    public async Task An_action_on_a_missing_transfer_returns_not_found()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Api.DispatchAsync(999_999)).StatusCode);
    }

    private Task<HttpResponseMessage> Act(string action, int id) => action switch
    {
        "dispatch" => Api.DispatchAsync(id),
        "receive" => Api.ReceiveAsync(id),
        "cancel" => Api.CancelAsync(id),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };
}
