using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockTransfer.Api.Data;
using Testcontainers.PostgreSql;

namespace StockTransfer.Tests.Infrastructure;

/// <summary>
/// Hosts the real API against a throwaway PostgreSQL container. The concurrency rules depend on PostgreSQL's row
/// locking, so the tests use the real database rather than an in-memory provider.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17").Build();

    public Task InitializeAsync() => _db.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
    }

    /// <summary>Removes all transfers and puts every stock row back to its seeded quantity.</summary>
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Transfers.ExecuteDeleteAsync(); // lines go with them (cascade)
        foreach (var seeded in SeedData.Stock())
        {
            await db.Stock
                .Where(s => s.BranchId == seeded.BranchId && s.ProductId == seeded.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, seeded.Quantity));
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }
}

/// <summary>One container for the whole run. Tests in a collection run one at a time, so each can reset the data.</summary>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "API with PostgreSQL";
}

public abstract class ApiTest(ApiFactory factory) : IAsyncLifetime
{
    protected TransferApi Api { get; } = new(factory.CreateClient());

    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
