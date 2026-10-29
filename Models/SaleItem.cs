namespace InventoryApi.Models;

public class SaleItem
{
    public int Id { get; set; }

    // Relationship to the parent sale.
    public int SaleId { get; set; }

    // Product being sold.
    public int ProductId { get; set; }

    // Inventory location the product was issued from.
    public int FromLocationId { get; set; }

    public int Quantity { get; set; }

    // Historical product information at time of sale.
    public string ProductNameSnapshot { get; set; }
        = string.Empty;

    public string CategorySnapshot { get; set; }
        = string.Empty;

    // Historical prices at time of sale.
    public decimal UnitSellingPrice { get; set; }

    public decimal UnitPurchaseCost { get; set; }

    // Linked inventory deduction, assigned when the sale is saved.
    public int? StockMovementId { get; set; }
}