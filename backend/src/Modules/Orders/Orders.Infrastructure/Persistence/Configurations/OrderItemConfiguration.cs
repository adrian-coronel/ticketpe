using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");

        // Value Object embebido — EF Core 8 lo guarda como columnas planas
        // de la misma tabla, no como una tabla aparte.
        builder.ComplexProperty(i => i.SeatId, seat =>
        {
            seat.Property(s => s.Zone).HasColumnName("zone").HasMaxLength(50);
            seat.Property(s => s.Row).HasColumnName("row").HasMaxLength(10);
            seat.Property(s => s.Number).HasColumnName("seat_number");
        });

        builder.ComplexProperty(i => i.Price, price =>
        {
            price.Property(p => p.Amount).HasColumnName("price").HasColumnType("numeric(12,2)");
            price.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3);
        });
    }
}

