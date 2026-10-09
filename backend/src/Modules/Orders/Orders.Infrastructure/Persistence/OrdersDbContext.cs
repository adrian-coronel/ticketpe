using Microsoft.EntityFrameworkCore;
using Orders.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Infrastructure.Persistence;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aplica automáticamente TODAS las clases IEntityTypeConfiguration<T>
        // de este proyecto. Agregas Inventory o Catalog después y no tocas esta línea.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
    }
}
