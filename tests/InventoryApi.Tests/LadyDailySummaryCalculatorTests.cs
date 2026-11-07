using InventoryApi.Models;
using InventoryApi.Services;
using Xunit;

namespace InventoryApi.Tests;

public class LadyDailySummaryCalculatorTests
{
    [Fact]
    public void MultipleReceipts_CombinesAmountsByLady()
    {
        // Arrange: two receipts containing three allocations.
        var allocations = new[]
        {
            new SaleAllocation
            {
                SaleId = 1,
                Position = 1,
                LadyNameSnapshot = "Hana",
                AllocatedEligibleSales = 200m,
                AllocatedBottleCost = 90m,
                AllocatedBack = 15.75m
            },
            new SaleAllocation
            {
                SaleId = 1,
                Position = 2,
                LadyNameSnapshot = "Yuna",
                AllocatedEligibleSales = 200m,
                AllocatedBottleCost = 90m,
                AllocatedBack = 15.75m
            },
            new SaleAllocation
            {
                SaleId = 2,
                Position = 1,
                LadyNameSnapshot = "Hana",
                AllocatedEligibleSales = 50m,
                AllocatedBottleCost = 15m,
                AllocatedBack = 4.50m
            }
        };

        // Act
        var summary =
            LadyDailySummaryCalculator.Calculate(allocations);

        // Assert: one row per lady.
        Assert.Equal(2, summary.Count);

        // Hana has the highest total sales.
        Assert.Equal("Hana", summary[0].Lady);

        var hana = summary.Single(row =>
            row.Lady == "Hana");

        Assert.Equal(250m, hana.Sales);
        Assert.Equal(105m, hana.TotalBottleCost);
        Assert.Equal(145m, hana.NetSales);
        Assert.Equal(20.25m, hana.Back);

        var yuna = summary.Single(row =>
            row.Lady == "Yuna");

        Assert.Equal(200m, yuna.Sales);
        Assert.Equal(90m, yuna.TotalBottleCost);
        Assert.Equal(110m, yuna.NetSales);
        Assert.Equal(15.75m, yuna.Back);
    }

    [Fact]
    public void NamesWithDifferentCapitalization_AreCombined()
    {
        var allocations = new[]
        {
            new SaleAllocation
            {
                LadyNameSnapshot = "Hana",
                AllocatedEligibleSales = 100m,
                AllocatedBottleCost = 30m,
                AllocatedBack = 5m
            },
            new SaleAllocation
            {
                LadyNameSnapshot = " hana ",
                AllocatedEligibleSales = 50m,
                AllocatedBottleCost = 10m,
                AllocatedBack = 2m
            }
        };

        var summary =
            LadyDailySummaryCalculator.Calculate(allocations);

        var row = Assert.Single(summary);

        Assert.Equal("Hana", row.Lady);
        Assert.Equal(150m, row.Sales);
        Assert.Equal(40m, row.TotalBottleCost);
        Assert.Equal(110m, row.NetSales);
        Assert.Equal(7m, row.Back);
    }

    [Fact]
    public void NoAllocations_ReturnsEmptySummary()
    {
        var summary = LadyDailySummaryCalculator.Calculate(
            Array.Empty<SaleAllocation>());

        Assert.Empty(summary);
    }
}