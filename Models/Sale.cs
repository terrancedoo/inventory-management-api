namespace InventoryApi.Models;

public class Sale
{
    public int Id { get; set; }

    // When the sale was recorded, in UTC.
    public DateTime SoldAtUtc { get; set; }

    // Singapore business date, using a 3 AM cutoff.
    public DateOnly BusinessDate { get; set; }

    // A single sale may contain multiple products.
    public ICollection<SaleItem> Items { get; set; }
        = new List<SaleItem>();

    // Daily sequence number, e.g. 1, 2, 3.
    public int? ReceiptSequence { get; set; }

    // Display receipt number, e.g. 20261105-001.
    public string? ReceiptNumber { get; set; }

    // Amount excluded from lady sales credit.
    // Nullable to distinguish historical sales where it was
    // never recorded from sales with zero exclusions.
    public decimal? ExcludedAmount { get; set; }

    // The ladies allocated to this receipt.
    public List<SaleAllocation> Allocations { get; set; }
        = new();
}