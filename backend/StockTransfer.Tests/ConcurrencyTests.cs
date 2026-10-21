using System.Net;
using StockTransfer.Api.Contracts;
using StockTransfer.Tests.Infrastructure;
using static StockTransfer.Api.Data.SeedData;

namespace StockTransfer.Tests;

/// <summary>
/// Requests sent at the same moment, the case a plain "read the status, then write" check gets wrong.
/// </summary>
[Collection(ApiCollection.Name)]
public class ConcurrencyTests(ApiFactory factory) : ApiTest(factory)
{
    private const int Copies = 10;

    [Fact]
    public async Task Concurrent_dispatches_of_one_transfer_move_stock_once()
    {
        var transfer = await Api.CreateAsync(WarehouseId, KandyId, (RiceId, 10));

        var responses = await TransferApi.AllAtOnce(Copies, () => Api.DispatchAsync(transfer.Id));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(490, await Api.StockAsync(WarehouseId, RiceId));
    }

    [Fact]
    public async Task Concurrent_receives_of_one_transfer_add_stock_once()
    {
        var transfer = await Api.CreateAsync(WarehouseId, KandyId, (RiceId, 10));
        await Api.DispatchAsync(transfer.Id);

        var responses = await TransferApi.AllAtOnce(Copies, () => Api.ReceiveAsync(transfer.Id));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(15, await Api.StockAsync(KandyId, RiceId));
    }

    [Fact]
    public async Task Concurrent_dispatches_competing_for_the_same_stock_never_take_it_below_zero()
    {
        // Galle has 3 coconut oil. Each of these transfers needs 2, so only one can be dispatched.
        var transfers = new List<TransferDto>();
        for (var i = 0; i < 3; i++)
        {
            transfers.Add(await Api.CreateAsync(GalleId, ColomboId, (CoconutOilId, 2)));
        }

        var responses = await Task.WhenAll(transfers.Select(t => Task.Run(() => Api.DispatchAsync(t.Id))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await Api.StockAsync(GalleId, CoconutOilId));
    }

    [Fact]
    public async Task Concurrent_creates_with_one_idempotency_key_make_one_transfer()
    {
        var request = new CreateTransferRequest(WarehouseId, ColomboId, [new CreateTransferLineRequest(SugarId, 3)]);

        var responses = await TransferApi.AllAtOnce(Copies, () => Api.PostCreateAsync(request, "same-click"));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(Copies - 1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        var ids = await Task.WhenAll(responses.Select(async r => (await TransferApi.ReadAsync<TransferDto>(r)).Id));
        Assert.Single(ids.Distinct());
        Assert.Single(await Api.ListAsync());
    }
}
