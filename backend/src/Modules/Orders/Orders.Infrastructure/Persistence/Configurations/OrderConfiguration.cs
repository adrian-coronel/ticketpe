using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain;

namespace Orders.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.UserId).HasColumnName("user_id");
        builder.Property(o => o.EventId).HasColumnName("event_id");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion<string>()      // guarda "Pending", no 0 — mucho más legible en la BD
            .HasMaxLength(20);

        builder.Ignore(o => o.Total);     // calculado, no es una columna

        // La lista es privada (_items): le decimos a EF que la lea/escriba
        // directamente por el campo, no por una propiedad pública que no existe.
        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey("OrderId")     // shadow property: no existe en el C#, solo en la tabla
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
