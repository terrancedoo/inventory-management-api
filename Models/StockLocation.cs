namespace InventoryApi.Models;

public class StockLocation
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsUsable { get; set; }
}