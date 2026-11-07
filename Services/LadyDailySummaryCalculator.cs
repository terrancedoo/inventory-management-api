using InventoryApi.Models;

namespace InventoryApi.Services;

public static class LadyDailySummaryCalculator
{
    public static List<LadyDailySummaryRow> Calculate(
        IEnumerable<SaleAllocation> allocations)
    {
        ArgumentNullException.ThrowIfNull(allocations);

        // Group allocations by lady name.
        // Ignore capitalization and surrounding spaces.
        var summary = allocations
            .GroupBy(
                a => a.LadyNameSnapshot.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                // Add up the lady's amounts across receipts.
                decimal sales = group.Sum(a =>
                    a.AllocatedEligibleSales);

                decimal bottleCost = group.Sum(a =>
                    a.AllocatedBottleCost);

                decimal back = group.Sum(a =>
                    a.AllocatedBack);

                decimal netSales = sales - bottleCost;

                return new LadyDailySummaryRow(
                    Lady: group.Key,
                    Sales: sales,
                    TotalBottleCost: bottleCost,
                    NetSales: netSales,
                    Back: back
                );
            })
            // Highest sales appear first.
            .OrderByDescending(row => row.Sales)
            .ThenBy(
                row => row.Lady,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        return summary;
    }
}