namespace InventoryApi.Models;

public class CreateSaleRequest
{
    public List<CreateSaleItemRequest> Items { get; set; }
        = new();
}