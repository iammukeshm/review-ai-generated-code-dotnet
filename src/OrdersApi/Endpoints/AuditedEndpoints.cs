using Microsoft.EntityFrameworkCore;
using OrdersApi.Data;

namespace OrdersApi.Endpoints;

// The same endpoint after the audit pass.
public static class AuditedEndpoints
{
    private const int MaxPageSize = 100;

    public static void MapAuditedEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v2/customers/{customerId:int}/orders", async (
            int customerId,
            OrderStatus? status,
            AppDbContext db,
            CancellationToken cancellationToken,
            int page = 1,
            int pageSize = 20) =>
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var customerExists = await db.Customers
                .AnyAsync(c => c.Id == customerId, cancellationToken);
            if (!customerExists)
            {
                return Results.NotFound();
            }

            var query = db.Orders
                .AsNoTracking()
                .Where(o => o.CustomerId == customerId);

            if (status is not null)
            {
                query = query.Where(o => o.Status == status);
            }

            var result = await query
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderSummary(
                    o.Id,
                    o.CreatedAt,
                    o.Status,
                    o.Items.Count,
                    o.Items.Sum(i => i.Quantity * i.UnitPrice)))
                .ToListAsync(cancellationToken);

            return Results.Ok(result);
        });
    }
}
