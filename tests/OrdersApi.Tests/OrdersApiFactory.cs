using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OrdersApi.Data;
using Testcontainers.PostgreSql;

namespace OrdersApi.Tests;

// One real PostgreSQL container for the whole test run.
public sealed class OrdersApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public SqlCommandCounter SqlCommands { get; } = new();

    public string ConnectionString => _postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Orders", _postgres.GetConnectionString());
        builder.ConfigureTestServices(services =>
            services.AddSingleton<IInterceptor>(SqlCommands));
    }

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await TestData.SeedAsync(db);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(nameof(OrdersApiCollection))]
public sealed class OrdersApiCollection : ICollectionFixture<OrdersApiFactory>;
