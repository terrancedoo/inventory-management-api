namespace InventoryApi.Models;

public class CreateStockMovementRequest
{
    public int ProductId { get; set; }

    public int? FromLocationId { get; set; }

    public int? ToLocationId { get; set; }

    public int Quantity { get; set; }

    public string? Reference { get; set; }
}