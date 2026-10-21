using System.Net;
using StockTransfer.Api.Contracts;
using StockTransfer.Tests.Infrastructure;
using static StockTransfer.Api.Data.SeedData;

namespace StockTransfer.Tests;

[Collection(ApiCollection.Name)]
public class CreateTransferTests(ApiFactory factory) : ApiTest(factory)
{
    [Theory]
    [InlineData("""{"fromBranchId":2,"toBranchId":2,"lines":[{"productId":1,"quantity":1}]}""",
        "The from and to branches must be different.")]
    [InlineData("""{"fromBranchId":1,"toBranchId":2,"lines":[{"productId":1,"quantity":0}]}""",
        "The quantity of Rice 5kg must be greater than zero.")]
    [InlineData("""{"fromBranchId":1,"toBranchId":2,"lines":[{"productId":1,"quantity":-5}]}""",
        "The quantity of Rice 5kg must be greater than zero.")]
    [InlineData("""{"fromBranchId":1,"toBranchId":2,"lines":[]}""",
        "A transfer needs at least one product line.")]
    [InlineData("""{"fromBranchId":1,"toBranchId":2}""",
        "A transfer needs at least one product line.")]
    [InlineData("""{"fromBranchId":1,"toBranchId":2,"lines":[{"productId":1,"quantity":1},{"productId":1,"quantity":2}]}""",
        "Rice 5kg is on more than one line. Put each product on one line.")]
    [InlineData("""{"fromBranchId":1,"toBranchId":99,"lines":[{"productId":1,"quantity":1}]}""",
        "To branch 99 does not exist.")]
    [InlineData("""{"fromBranchId":1,"toBranchId":2,"lines":[{"productId":42,"quantity":1}]}""",
        "Product 42 does not exist.")]
    public async Task An_invalid_request_is_rejected_with_a_clear_message_and_creates_nothing(string json, string expectedMessage)
    {
        var response = await Api.PostCreateJsonAsync(json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedMessage, (await TransferApi.ReadAsync<Problem>(response)).Messages);
        Assert.Empty(await Api.ListAsync());
    }

    [Fact]
    public async Task Every_problem_with_a_request_is_reported_at_once()
    {
        var response = await Api.PostCreateJsonAsync(
            """{"fromBranchId":3,"toBranchId":3,"lines":[{"productId":2,"quantity":0}]}""");

        var problem = await TransferApi.ReadAsync<Problem>(response);
        Assert.Equal(
            ["The from and to branches must be different.", "The quantity of Red Dhal 1kg must be greater than zero."],
            problem.Messages);
    }

    [Fact]
    public async Task Repeating_a_create_with_the_same_idempotency_key_returns_the_original_transfer()
    {
        var request = new CreateTransferRequest(WarehouseId, ColomboId, [new CreateTransferLineRequest(SugarId, 3)]);

        var first = await Api.PostCreateAsync(request, "retry-me");
        var retry = await Api.PostCreateAsync(request, "retry-me");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(
            (await TransferApi.ReadAsync<TransferDto>(first)).Id,
            (await TransferApi.ReadAsync<TransferDto>(retry)).Id);
        Assert.Single(await Api.ListAsync());
    }
}
