using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StockTransfer.Api.Contracts;
using StockTransfer.Api.Domain;

namespace StockTransfer.Tests.Infrastructure;

public record Problem(int Status, string Title, string Detail, List<string> Messages);

/// <summary>A thin typed wrapper over the HTTP API, so the tests read as the rules they check.</summary>
public class TransferApi(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<TransferDto> CreateAsync(int fromBranchId, int toBranchId, params (int ProductId, int Quantity)[] lines)
    {
        var response = await PostCreateAsync(new CreateTransferRequest(
            fromBranchId, toBranchId, lines.Select(l => new CreateTransferLineRequest(l.ProductId, l.Quantity)).ToList()));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<TransferDto>(response);
    }

    public Task<HttpResponseMessage> PostCreateAsync(CreateTransferRequest request, string? idempotencyKey = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "transfers") { Content = JsonContent.Create(request, options: Json) };
        if (idempotencyKey is not null)
        {
            message.Headers.Add("Idempotency-Key", idempotencyKey);
        }
        return http.SendAsync(message);
    }

    public Task<HttpResponseMessage> PostCreateJsonAsync(string json) =>
        http.PostAsync("transfers", new StringContent(json, Encoding.UTF8, "application/json"));

    public Task<HttpResponseMessage> DispatchAsync(int id) => http.PostAsync($"transfers/{id}/dispatch", null);

    public Task<HttpResponseMessage> ReceiveAsync(int id) => http.PostAsync($"transfers/{id}/receive", null);

    public Task<HttpResponseMessage> CancelAsync(int id) => http.PostAsync($"transfers/{id}/cancel", null);

    /// <summary>Sends <paramref name="count"/> copies of a request at once, as a double-click or retry storm would.</summary>
    public static Task<HttpResponseMessage[]> AllAtOnce(int count, Func<Task<HttpResponseMessage>> send) =>
        Task.WhenAll(Enumerable.Range(0, count).Select(_ => Task.Run(send)));

    public async Task<TransferStatus> StatusAsync(int id) =>
        (await http.GetFromJsonAsync<TransferDto>($"transfers/{id}", Json))!.Status;

    public async Task<IReadOnlyList<TransferDto>> ListAsync() =>
        (await http.GetFromJsonAsync<List<TransferDto>>("transfers", Json))!;

    public async Task<int> StockAsync(int branchId, int productId)
    {
        var stock = await http.GetFromJsonAsync<BranchStockDto>($"branches/{branchId}/stock", Json);
        return stock!.Items.Single(i => i.ProductId == productId).Quantity;
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;
}
