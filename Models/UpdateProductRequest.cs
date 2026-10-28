namespace InventoryApi.Models;

public class UpdateProductRequest
{
    public string? Name { get; set; }

    public string? Category { get; set; }

    public decimal? PurchaseCost { get; set; }

    public decimal? SellingPrice { get; set; }
}