namespace InventoryApi.Models;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal PurchaseCost { get; set; }

    public decimal SellingPrice { get; set; }

}