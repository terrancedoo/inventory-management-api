namespace InventoryApi.Services;

public static class AllocationSplitter
{
    public static decimal[] Split(
        decimal total,
        int count,
        int decimalPlaces = 2)
    {
        if (count < 1 || count > 8)
            throw new ArgumentOutOfRangeException(
                nameof(count), "Count must be between 1 and 8.");

        decimal scale = decimalPlaces switch
        {
            2 => 100m,
            4 => 10000m,
            _ => throw new ArgumentOutOfRangeException(
                nameof(decimalPlaces))
        };

        // Convert money into the smallest allowed units.
        decimal scaledTotal = total * scale;

        if (scaledTotal != decimal.Truncate(scaledTotal))
            throw new ArgumentException(
                "Total has too many decimal places.");

        long units = checked((long)scaledTotal);

        long baseUnits = units / count;
        long remainder = units % count;

        // Distribute rounding remainders deterministically.
        return Enumerable.Range(0, count)
            .Select(index =>
                (baseUnits +
                    (index < Math.Abs(remainder)
                        ? Math.Sign(remainder)
                        : 0)) / scale)
            .ToArray();
    }
}