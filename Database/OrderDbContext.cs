using Microsoft.EntityFrameworkCore;
using ordersAPI.Entities;

namespace ordersAPI.Database;

public class OrdersDBContext : DbContext
{
    public OrdersDBContext(DbContextOptions<OrdersDBContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.Status)
                .HasColumnName("status");
        });

        base.OnModelCreating(modelBuilder);
    }
}