namespace InventoryApi.Models;

public sealed record LadyDailySummaryRow(
    string Lady,
    decimal Sales,
    decimal TotalBottleCost,
    decimal NetSales,
    decimal Back
);