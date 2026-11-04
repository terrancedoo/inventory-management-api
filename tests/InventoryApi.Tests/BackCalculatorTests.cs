using InventoryApi.Services;
using Xunit;

namespace InventoryApi.Tests;

public class BackCalculatorTests
{
    [Theory]
    [InlineData("Champagne", true)]
    [InlineData("Red Wine", true)]
    [InlineData("White Wine", true)]
    [InlineData("Red & White Wine", true)]
    [InlineData("Tequila", true)]
    [InlineData("Sake", true)]
    [InlineData("Beer", false)]
    [InlineData("Whisky", false)]
    [InlineData("Vodka", false)]
    public void CategoryEligibility_IsCorrect(
        string category,
        bool expected)
    {
        bool result =
            BackCalculator.IsEligibleCategory(category);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Champagne_CalculatesBackCorrectly()
    {
        decimal result = BackCalculator.Calculate(
            "Champagne",
            180m,
            75m,
            2);

        Assert.Equal(31.50m, result);
    }

    [Fact]
    public void Beer_ReturnsZeroBack()
    {
        decimal result = BackCalculator.Calculate(
            "Beer",
            20m,
            8m,
            5);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void InvalidQuantity_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BackCalculator.Calculate(
                "Champagne",
                180m,
                75m,
                0));
    }
}