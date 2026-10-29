using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryApi.Data;
using InventoryApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InventoryApi.Tests;

public class SalesEndpointsTests
{
    [Fact]
    public async Task MultiItemSale_DeductsStockAndSavesPrices()
    {
        // Arrange: Create isolated test database.
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        // Create two products.
        var champagneResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Test Champagne",
                category = "Champagne",
                purchaseCost = 75m,
                sellingPrice = 180m
            });

        var wineResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Test Wine",
                category = "Wine",
                purchaseCost = 30m,
                sellingPrice = 80m
            });

        Assert.Equal(HttpStatusCode.Created,
            champagneResponse.StatusCode);

        Assert.Equal(HttpStatusCode.Created,
            wineResponse.StatusCode);

        var champagne = await champagneResponse.Content
            .ReadFromJsonAsync<Product>();

        var wine = await wineResponse.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(champagne);
        Assert.NotNull(wine);

        // Receive four Champagne into Store.
        var receipt1 = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = champagne.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)1,
                quantity = 4
            });

        // Receive two Wine into Wine Chiller.
        var receipt2 = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = wine.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)4,
                quantity = 2
            });

        Assert.Equal(HttpStatusCode.Created,
            receipt1.StatusCode);

        Assert.Equal(HttpStatusCode.Created,
            receipt2.StatusCode);

        // Act: Sell two Champagne and one Wine.
        var saleResponse = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                items = new object[]
                {
                    new
                    {
                        productId = champagne.Id,
                        fromLocationId = 1,
                        quantity = 2
                    },
                    new
                    {
                        productId = wine.Id,
                        fromLocationId = 4,
                        quantity = 1
                    }
                }
            });

        // Assert: Sale created successfully.
        Assert.Equal(HttpStatusCode.Created,
            saleResponse.StatusCode);

        var sale = await saleResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(440m,
            sale.GetProperty("totalRevenue").GetDecimal());

        Assert.Equal(180m,
            sale.GetProperty("totalCost").GetDecimal());

        Assert.Equal(260m,
            sale.GetProperty("grossProfit").GetDecimal());

        Assert.Equal(2,
            sale.GetProperty("items").GetArrayLength());

        // Check remaining stock.
        var champagneStock =
            await client.GetFromJsonAsync<JsonElement>(
                $"/products/{champagne.Id}/stock");

        var wineStock =
            await client.GetFromJsonAsync<JsonElement>(
                $"/products/{wine.Id}/stock");

        Assert.Equal(2,
            champagneStock.GetProperty("totalStock").GetInt32());

        Assert.Equal(1,
            wineStock.GetProperty("totalStock").GetInt32());

        // Verify stored price snapshots and stock links.
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        var saleItems = await db.SaleItems
            .AsNoTracking()
            .ToListAsync();

        Assert.Equal(2, saleItems.Count);

        Assert.All(saleItems,
            item => Assert.NotNull(item.StockMovementId));

        var champagneItem = saleItems.Single(i =>
            i.ProductId == champagne.Id);

        Assert.Equal(180m,
            champagneItem.UnitSellingPrice);

        Assert.Equal(75m,
            champagneItem.UnitPurchaseCost);
    }

    [Fact]
    public async Task InsufficientStock_RejectsEntireSale()
    {
        // Arrange
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        var productResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Stock Validation Test",
                category = "Champagne",
                purchaseCost = 75m,
                sellingPrice = 180m
            });

        Assert.Equal(HttpStatusCode.Created,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(product);

        // Receive three bottles.
        var receipt = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = product.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)1,
                quantity = 3
            });

        Assert.Equal(HttpStatusCode.Created,
            receipt.StatusCode);

        // Act: Request four bottles across two lines.
        var saleResponse = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                items = new object[]
                {
                    new
                    {
                        productId = product.Id,
                        fromLocationId = 1,
                        quantity = 2
                    },
                    new
                    {
                        productId = product.Id,
                        fromLocationId = 1,
                        quantity = 2
                    }
                }
            });

        // Assert: Entire sale rejected.
        Assert.Equal(HttpStatusCode.BadRequest,
            saleResponse.StatusCode);

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        // No sale records should exist.
        Assert.Empty(await db.Sales.ToListAsync());
        Assert.Empty(await db.SaleItems.ToListAsync());

        // Only the original receipt should exist.
        var movements = await db.StockMovements
            .Where(m => m.ProductId == product.Id)
            .ToListAsync();

        Assert.Single(movements);

        // Inventory remains unchanged.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{product.Id}/stock");

        Assert.Equal(3,
            stock.GetProperty("totalStock").GetInt32());
    }
}