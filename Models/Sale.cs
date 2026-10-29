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
}