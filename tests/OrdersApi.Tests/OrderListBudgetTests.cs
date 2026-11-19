using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrdersApi.Endpoints;

namespace OrdersApi.Tests;

// Audit findings turned into tests, so the same problems cannot come back.
[Collection(nameof(OrdersApiCollection))]
public class OrderListBudgetTests(OrdersApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("v1", Skip = "The first draft runs 22 SQL commands for one page. Remove Skip to see it fail.")]
    [InlineData("v2")]
    public async Task Order_list_runs_at_most_two_sql_commands(string version)
    {
        factory.SqlCommands.Reset();

        var response = await _client.GetAsync(
            $"/{version}/customers/{TestData.BigCustomerId}/orders?pageSize=20",
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        Assert.True(
            factory.SqlCommands.Count <= 2,
            $"Expected at most 2 SQL commands, but the endpoint ran {factory.SqlCommands.Count}.");
    }

    [Theory]
    [InlineData("v1", Skip = "The first draft has no page size limit. Remove Skip to see it fail.")]
    [InlineData("v2")]
    public async Task Page_size_is_capped_at_100(string version)
    {
        var response = await _client.GetAsync(
            $"/{version}/customers/{TestData.BigCustomerId}/orders?pageSize=100000",
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var orders = await response.Content.ReadFromJsonAsync<List<OrderSummary>>(Json, TestContext.Current.CancellationToken);
        Assert.Equal(100, orders!.Count);
    }
}
