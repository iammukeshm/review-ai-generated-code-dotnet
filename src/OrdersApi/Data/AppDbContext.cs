using Microsoft.EntityFrameworkCore;

namespace OrdersApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().Property(c => c.Name).HasMaxLength(200);

        modelBuilder.Entity<Order>(order =>
        {
            order.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

            // Added after the audit: the list endpoint filters by customer and sorts by date.
            order.HasIndex(o => new { o.CustomerId, o.CreatedAt });
        });

        modelBuilder.Entity<OrderItem>(item =>
        {
            item.Property(i => i.Sku).HasMaxLength(50);
            item.Property(i => i.UnitPrice).HasPrecision(18, 2);
        });
    }
}
