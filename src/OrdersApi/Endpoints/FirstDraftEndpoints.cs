using Microsoft.EntityFrameworkCore;
using OrdersApi.Data;

namespace OrdersApi.Endpoints;

// The first version of the endpoint. It returns the right data and passes every
// integration test, but it has the problems that the audit pass is there to catch.
public static class FirstDraftEndpoints
{
    public static void MapFirstDraftEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/customers/{customerId:int}/orders", async (
            int customerId,
            OrderStatus? status,
            AppDbContext db,
            int page = 1,
            int pageSize = 20) =>
        {
            var customer = await db.Customers.FindAsync(customerId);
            if (customer is null)
            {
                return Results.NotFound();
            }

            // Loads every order the customer has ever placed into memory.
            var orders = await db.Orders
                .Where(o => o.CustomerId == customerId)
                .ToListAsync();

            // Filtering, sorting and paging happen in memory instead of in SQL.
            var pageOfOrders = orders
                .Where(o => status is null || o.Status == status)
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.Id)
                .Skip((Math.Max(page, 1) - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = new List<OrderSummary>();
            foreach (var order in pageOfOrders)
            {
                // One extra query per order: the N+1 problem.
                var items = await db.OrderItems
                    .Where(i => i.OrderId == order.Id)
                    .ToListAsync();

                result.Add(new OrderSummary(
                    order.Id,
                    order.CreatedAt,
                    order.Status,
                    items.Count,
                    items.Sum(i => i.Quantity * i.UnitPrice)));
            }

            return Results.Ok(result);
        });
    }
}
