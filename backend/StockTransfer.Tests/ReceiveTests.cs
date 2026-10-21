using System.Net;
using StockTransfer.Api.Domain;
using StockTransfer.Tests.Infrastructure;
using static StockTransfer.Api.Data.SeedData;

namespace StockTransfer.Tests;

/// <summary>Receiving less than was sent, e.g. damaged goods: only what arrived is added, and the difference is kept.</summary>
[Collection(ApiCollection.Name)]
public class ReceiveTests(ApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task Receiving_less_than_was_sent_adds_only_what_arrived_and_records_the_difference()
    {
        var transfer = await Api.CreateAsync(WarehouseId, KandyId, (RiceId, 10), (TeaId, 5));
        await Api.DispatchAsync(transfer.Id);

        // Two bags of rice damaged; the tea line is left out, so it is received in full.
        var response = await Api.ReceiveAsync(transfer.Id, (RiceId, 8));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5 + 8, await Api.StockAsync(KandyId, RiceId));
        Assert.Equal(0 + 5, await Api.StockAsync(KandyId, TeaId));
        // The sender lost the full quantity at dispatch; the shortfall does not go back.
        Assert.Equal(490, await Api.StockAsync(WarehouseId, RiceId));

        var received = await Api.GetAsync(transfer.Id);
        Assert.Equal(TransferStatus.Received, received.Status);
        var rice = received.Lines.Single(l => l.ProductId == RiceId);
        Assert.Equal((10, 8), (rice.Quantity, rice.ReceivedQuantity));
        var tea = received.Lines.Single(l => l.ProductId == TeaId);
        Assert.Equal((5, 5), (tea.Quantity, tea.ReceivedQuantity));
    }

    [Fact]
    public async Task Receiving_with_no_body_receives_every_line_in_full()
    {
        var transfer = await Api.CreateAsync(WarehouseId, KandyId, (RiceId, 10));
        await Api.DispatchAsync(transfer.Id);
        Assert.Null((await Api.GetAsync(transfer.Id)).Lines.Single().ReceivedQuantity);

        await Api.ReceiveAsync(transfer.Id);

        Assert.Equal(10, (await Api.GetAsync(transfer.Id)).Lines.Single().ReceivedQuantity);
        Assert.Equal(15, await Api.StockAsync(KandyId, RiceId));
    }

    [Fact]
    public async Task Invalid_received_quantities_are_rejected_and_the_transfer_stays_dispatched()
    {
        var transfer = await Api.CreateAsync(WarehouseId, KandyId, (RiceId, 10), (TeaId, 5));
        await Api.DispatchAsync(transfer.Id);

        var response = await Api.ReceiveAsync(transfer.Id, (RiceId, 11), (TeaId, -1), (SugarId, 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await TransferApi.ReadAsync<Problem>(response);
        Assert.Equal(
            [
                $"Product {SugarId} is not on transfer {transfer.Id}.",
                "The received quantity of Rice 5kg must be between 0 and the 10 sent.",
                "The received quantity of Ceylon Tea 400g must be between 0 and the 5 sent.",
            ],
            problem.Messages);

        // The status change made in the same transaction was rolled back, so it can still be received properly.
        Assert.Equal(TransferStatus.Dispatched, await Api.StatusAsync(transfer.Id));
        Assert.Equal(5, await Api.StockAsync(KandyId, RiceId));
        Assert.Equal(HttpStatusCode.OK, (await Api.ReceiveAsync(transfer.Id, (RiceId, 10))).StatusCode);
        Assert.Equal(15, await Api.StockAsync(KandyId, RiceId));
    }

    [Fact]
    public async Task A_repeated_receive_with_different_quantities_changes_nothing()
    {
        var transfer = await Api.CreateAsync(WarehouseId, KandyId, (RiceId, 10));
        await Api.DispatchAsync(transfer.Id);
        await Api.ReceiveAsync(transfer.Id, (RiceId, 8));

        var repeat = await Api.ReceiveAsync(transfer.Id, (RiceId, 3));

        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal(8, (await Api.GetAsync(transfer.Id)).Lines.Single().ReceivedQuantity);
        Assert.Equal(13, await Api.StockAsync(KandyId, RiceId));
    }
}
