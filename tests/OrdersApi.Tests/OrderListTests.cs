using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrdersApi.Data;
using OrdersApi.Endpoints;

namespace OrdersApi.Tests;

// Behaviour tests. Both versions of the endpoint pass every one of these.
[Collection(nameof(OrdersApiCollection))]
public class OrderListTests(OrdersApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task Returns_orders_newest_first_with_item_counts_and_totals(string version)
    {
        var orders = await GetOrders(version, $"customers/{TestData.AliceId}/orders");

        Assert.Equal(3, orders.Count);
        Assert.Equal([TestData.BaseDate.AddDays(2), TestData.BaseDate.AddDays(1), TestData.BaseDate], orders.Select(o => o.CreatedAt));
        Assert.Equal([3, 2, 1], orders.Select(o => o.ItemCount));
        Assert.Equal([124.50m, 37.50m, 20.00m], orders.Select(o => o.Total));
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task Filters_by_status(string version)
    {
        var orders = await GetOrders(version, $"customers/{TestData.AliceId}/orders?status=Shipped");

        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.Equal(OrderStatus.Shipped, o.Status));
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task Returns_the_requested_page(string version)
    {
        var orders = await GetOrders(version, $"customers/{TestData.AliceId}/orders?page=2&pageSize=2");

        var order = Assert.Single(orders);
        Assert.Equal(TestData.BaseDate, order.CreatedAt);
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task Returns_an_empty_list_for_a_page_past_the_end(string version)
    {
        var orders = await GetOrders(version, $"customers/{TestData.AliceId}/orders?page=5&pageSize=2");

        Assert.Empty(orders);
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task Returns_an_empty_list_for_a_customer_with_no_orders(string version)
    {
        var orders = await GetOrders(version, $"customers/{TestData.NoOrdersCustomerId}/orders");

        Assert.Empty(orders);
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task Returns_404_for_an_unknown_customer(string version)
    {
        var response = await _client.GetAsync($"/{version}/customers/999999/orders", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task Returns_400_for_an_unknown_status(string version)
    {
        var response = await _client.GetAsync($"/{version}/customers/{TestData.AliceId}/orders?status=Lost", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<List<OrderSummary>> GetOrders(string version, string path)
    {
        var response = await _client.GetAsync($"/{version}/{path}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<OrderSummary>>(Json, TestContext.Current.CancellationToken))!;
    }
}
