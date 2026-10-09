using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Domain.ValueObjects
{
    public readonly record struct SeatId(string Zone, string Row, int Number);
}
