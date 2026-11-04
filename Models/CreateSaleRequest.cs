namespace InventoryApi.Models;

public class CreateSaleRequest
{
    public List<CreateSaleItemRequest> Items { get; set; }
        = new();

    // Amount excluded from ladies' sales credit.
    public decimal? ExcludedAmount { get; set; }

    // Up to eight ladies assigned to the receipt.
    public List<string>? LadyNames { get; set; }
}