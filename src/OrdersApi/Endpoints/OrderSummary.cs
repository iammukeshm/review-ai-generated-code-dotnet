using OrdersApi.Data;

namespace OrdersApi.Endpoints;

public record OrderSummary(int Id, DateTime CreatedAt, OrderStatus Status, int ItemCount, decimal Total);
