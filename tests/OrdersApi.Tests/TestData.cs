using Microsoft.EntityFrameworkCore;
using OrdersApi.Data;

namespace OrdersApi.Tests;

public static class TestData
{
    public static int AliceId { get; private set; }
    public static int NoOrdersCustomerId { get; private set; }
    public static int BigCustomerId { get; private set; }

    public static readonly DateTime BaseDate = new(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);

    public static async Task SeedAsync(AppDbContext db)
    {
        // Small, hand-written data for the behavior tests.
        var alice = new Customer
        {
            Name = "Alice",
            Orders =
            [
                NewOrder(BaseDate.AddDays(0), OrderStatus.Pending, (2, 10.00m)),
                NewOrder(BaseDate.AddDays(1), OrderStatus.Shipped, (1, 25.50m), (3, 4.00m)),
                NewOrder(BaseDate.AddDays(2), OrderStatus.Shipped, (5, 2.00m), (1, 100.00m), (2, 7.25m))
            ]
        };
        var noOrders = new Customer { Name = "No Orders Yet" };
        var big = new Customer { Name = "Big Customer" };

        db.Customers.AddRange(alice, noOrders, big);
        await db.SaveChangesAsync();

        AliceId = alice.Id;
        NoOrdersCustomerId = noOrders.Id;
        BigCustomerId = big.Id;

        // Volume for the audit: 5,000 orders for one customer and 40,000 orders
        // spread across 200 other customers, with 3 items per order.
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Customers" ("Name")
            SELECT 'Customer ' || g FROM generate_series(1, 200) AS g;

            INSERT INTO "Orders" ("CustomerId", "CreatedAt", "Status")
            SELECT {BigCustomerId},
                   TIMESTAMPTZ '2024-01-01' + (g * INTERVAL '1 hour'),
                   (ARRAY['Pending','Paid','Shipped','Cancelled'])[1 + g % 4]
            FROM generate_series(1, 5000) AS g;

            INSERT INTO "Orders" ("CustomerId", "CreatedAt", "Status")
            SELECT c."Id",
                   TIMESTAMPTZ '2024-01-01' + (g * INTERVAL '1 hour'),
                   (ARRAY['Pending','Paid','Shipped','Cancelled'])[1 + g % 4]
            FROM "Customers" c
            CROSS JOIN generate_series(1, 200) AS g
            WHERE c."Name" LIKE 'Customer %';

            INSERT INTO "OrderItems" ("OrderId", "Sku", "Quantity", "UnitPrice")
            SELECT o."Id", 'SKU-' || i, 1 + (o."Id" + i) % 5, 9.99
            FROM "Orders" o
            CROSS JOIN generate_series(1, 3) AS i
            WHERE o."CustomerId" <> {AliceId};
            """);

        await db.Database.ExecuteSqlRawAsync("ANALYZE;");
    }

    private static Order NewOrder(DateTime createdAt, OrderStatus status, params (int Quantity, decimal UnitPrice)[] items) =>
        new()
        {
            CreatedAt = createdAt,
            Status = status,
            Items = items
                .Select((item, index) => new OrderItem
                {
                    Sku = $"SKU-{index + 1}",
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                })
                .ToList()
        };
}
