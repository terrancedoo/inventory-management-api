namespace InventoryApi.Models;

public class StockMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int? FromLocationId { get; set; }

    public int? ToLocationId { get; set; }

    public int Quantity { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string? Reference { get; set; }
}