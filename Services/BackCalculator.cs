namespace InventoryApi.Services;

public static class BackCalculator
{
    private const decimal BackRate = 0.15m;

    public static bool IsEligibleCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        // Normalise case, spacing and punctuation.
        string normalised = new string(
            category
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());

        return normalised is
            "CHAMPAGNE" or
            "REDWINE" or
            "WHITEWINE" or
            "REDWHITEWINE" or
            "TEQUILA" or
            "SAKE";
    }

    public static decimal Calculate(
        string? category,
        decimal sellingPrice,
        decimal purchaseCost,
        int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity must be greater than zero.");
        }

        if (!IsEligibleCategory(category))
        {
            return 0m;
        }

        decimal profitPerUnit =
            sellingPrice - purchaseCost;

        return profitPerUnit * quantity * BackRate;
    }
}