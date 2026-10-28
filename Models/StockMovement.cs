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

    // The original movement being reversed.
    // Null means this is a normal movement.
    public int? ReversesMovementId { get; set; }

    // Explanation of why the reversal was performed.
    public string? ReversalReason { get; set; }
}