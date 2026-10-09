using Orders.Domain.Exceptions;
using Orders.Domain.ValueObjects;

namespace Orders.Domain;

public sealed class Order
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid EventId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<OrderItem> _items = new();
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    public Money Total => Money.Sum(_items.Select(i => i.Price));   // calculado, no se guarda

    private Order() { }   // EF Core lo necesita, aunque nunca lo llames tú

    public static Order Create(Guid userId, Guid eventId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        EventId = eventId,
        Status = OrderStatus.Pending,
        CreatedAt = DateTime.UtcNow,
    };

    public void AddTicket(SeatId seat, Money price)
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("No se puede modificar una orden cerrada.");
        if (_items.Count >= MaxTicketsPerOrder)
            throw new DomainException($"Máximo {MaxTicketsPerOrder} entradas por orden.");
        if (_items.Any(i => i.SeatId == seat))
            throw new DomainException("Ese asiento ya está en la orden.");

        _items.Add(OrderItem.Create(seat, price));
    }

    public void MarkAsPaid()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("Solo una orden pendiente puede marcarse como pagada.");
        Status = OrderStatus.Paid;
    }

    public void MarkAsFailed(string reason)
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("Solo una orden pendiente puede marcarse como fallida.");
        Status = OrderStatus.Failed;
    }

    public const int MaxTicketsPerOrder = 4;
}
