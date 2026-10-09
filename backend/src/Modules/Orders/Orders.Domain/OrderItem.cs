using Orders.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Domain
{
    public sealed class OrderItem
    {
        public Guid Id { get; private set; }
        public SeatId SeatId { get; private set; }
        public Money Price { get; private set; }

        private OrderItem() { }   // EF Core

        public static OrderItem Create(SeatId seat, Money price) => new()
        {
            Id = Guid.NewGuid(),
            SeatId = seat,
            Price = price,
        };
    }
}
