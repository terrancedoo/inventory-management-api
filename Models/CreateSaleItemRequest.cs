namespace InventoryApi.Models;

public class CreateSaleItemRequest
{
    public int ProductId { get; set; }

    public int FromLocationId { get; set; }

    public int Quantity { get; set; }
}