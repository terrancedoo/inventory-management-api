namespace InventoryApi.Models;

public class SaleAllocation
{
    public int Id { get; set; }

    // Parent receipt.
    public int SaleId { get; set; }

    // Position from 1 to 8 on the receipt.
    public int Position { get; set; }

    // Preserve the name used when the sale occurred.
    public string LadyNameSnapshot { get; set; }
        = string.Empty;

    // Financial values assigned to this lady.
    public decimal AllocatedEligibleSales { get; set; }

    public decimal AllocatedBottleCost { get; set; }

    public decimal AllocatedBack { get; set; }

    // Relationship to the sale.
    public Sale Sale { get; set; } = null!;
}