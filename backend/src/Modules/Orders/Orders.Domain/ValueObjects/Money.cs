using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Domain.ValueObjects
{
    public readonly record struct Money(decimal Amount, string Currency)
    {
        public static Money Sum(IEnumerable<Money> values)
        {
            var list = values.ToList();
            if (list.Count == 0) return new Money(0, "PEN");
            return new Money(list.Sum(m => m.Amount), list[0].Currency);
        }
    }
}
