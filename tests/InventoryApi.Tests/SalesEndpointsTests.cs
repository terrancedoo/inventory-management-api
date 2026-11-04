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

        // Verify total BACK in the POST /sales response.
        Assert.Equal(31.50m,
            sale.GetProperty("totalBack").GetDecimal());

        // Verify BACK for each item in the response.
        var responseItems = sale.GetProperty("items")
            .EnumerateArray()
            .ToList();

        var champagneResponseItem = responseItems.Single(item =>
            item.GetProperty("productId").GetInt32()
                == champagne.Id);

        var wineResponseItem = responseItems.Single(item =>
            item.GetProperty("productId").GetInt32()
                == wine.Id);

        Assert.Equal(31.50m,
            champagneResponseItem
                .GetProperty("backAmount").GetDecimal());

        Assert.Equal(0m,
            wineResponseItem
                .GetProperty("backAmount").GetDecimal());

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

        // Verify BACK for Champagne.
        Assert.Equal("Champagne",
            champagneItem.CategorySnapshot);

        Assert.NotNull(champagneItem.BackAmount);

        Assert.Equal(31.50m,
            champagneItem.BackAmount.Value);

        // Verify BACK for Wine.
        // The generic "Wine" category is not BACK eligible.
        // Red Wine and White Wine are eligible.
        var wineItem = saleItems.Single(i =>
            i.ProductId == wine.Id);

        Assert.Equal("Wine",
            wineItem.CategorySnapshot);

        Assert.NotNull(wineItem.BackAmount);

        Assert.Equal(0m,
            wineItem.BackAmount.Value);

        // Verify total BACK for the entire receipt.
        Assert.All(saleItems,
            item => Assert.NotNull(item.BackAmount));

        decimal totalBack = saleItems.Sum(
            item => item.BackAmount!.Value);

        Assert.Equal(31.50m, totalBack);

        // Change the product after the sale has completed.
        using var updateResponse = await client.PutAsJsonAsync(
            $"/products/{champagne.Id}",
            new
            {
                name = "Updated Test Champagne",
                category = "Beer",
                purchaseCost = 120m,
                sellingPrice = 250m
            });

        Assert.Equal(
            HttpStatusCode.NoContent,
            updateResponse.StatusCode);

        // Read the historical sale item from SQLite again.
        var savedChampagneItem = await db.SaleItems
            .AsNoTracking()
            .SingleAsync(i => i.Id == champagneItem.Id);

        // The recorded sale must preserve its original category.
        Assert.Equal("Champagne",
            savedChampagneItem.CategorySnapshot);

        // Historical selling price and cost remain unchanged.
        Assert.Equal(180m,
            savedChampagneItem.UnitSellingPrice);

        Assert.Equal(75m,
            savedChampagneItem.UnitPurchaseCost);

        // BACK remains S$31.50 despite the product changes.
        Assert.NotNull(savedChampagneItem.BackAmount);

        Assert.Equal(31.50m,
            savedChampagneItem.BackAmount.Value);

        // Retrieve the historical receipt after updating
        // the Champagne product's price and category.
        int saleId = sale.GetProperty("id").GetInt32();

        using var historicalResponse = await client.GetAsync(
            $"/sales/{saleId}");

        Assert.Equal(
            HttpStatusCode.OK,
            historicalResponse.StatusCode);

        var historicalReceipt = await historicalResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        // Verify the original financial totals.
        Assert.Equal(440m,
            historicalReceipt.GetProperty("totalRevenue").GetDecimal());

        Assert.Equal(260m,
            historicalReceipt.GetProperty("grossProfit").GetDecimal());

        Assert.Equal(31.50m,
            historicalReceipt.GetProperty("totalBack").GetDecimal());

        // Locate the Champagne in the historical receipt.
        var historicalItems = historicalReceipt.GetProperty("items")
            .EnumerateArray()
            .ToList();

        var historicalChampagne = historicalItems.Single(item =>
            item.GetProperty("productId").GetInt32()
                == champagne.Id);

        // Verify that the original product information remains.
        Assert.Equal("Champagne",
            historicalChampagne
                .GetProperty("categorySnapshot").GetString());

        Assert.Equal(180m,
            historicalChampagne
                .GetProperty("unitSellingPrice").GetDecimal());

        Assert.Equal(75m,
            historicalChampagne
                .GetProperty("unitPurchaseCost").GetDecimal());

        Assert.Equal(31.50m,
            historicalChampagne
                .GetProperty("backAmount").GetDecimal());
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

        // A failed sale must not consume a receipt number.
        // Create a valid sale after the rejected sale.
        var validSaleResponse = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                items = new[]
                {
            new
            {
                productId = product.Id,
                fromLocationId = 1,
                quantity = 1
            }
                }
            });

        Assert.Equal(
            HttpStatusCode.Created,
            validSaleResponse.StatusCode);

        var validSale = await validSaleResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        // Since the previous sale failed,
        // the first successful receipt should be sequence 1.
        Assert.Equal(1,
            validSale.GetProperty("receiptSequence").GetInt32());

        Assert.Matches(
            @"^\d{8}-001$",
            validSale.GetProperty("receiptNumber").GetString()!);
    }

    [Fact]
    public async Task ConsecutiveSales_GenerateUniqueReceiptNumbers()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        // Create a product.
        var productResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Receipt Test Champagne",
                category = "Champagne",
                purchaseCost = 75m,
                sellingPrice = 180m
            });

        Assert.Equal(HttpStatusCode.Created,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(product);

        // Receive enough inventory for two sales.
        var stockResponse = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = product.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)1,
                quantity = 5
            });

        Assert.Equal(HttpStatusCode.Created,
            stockResponse.StatusCode);

        // Sale request.
        var request = new
        {
            items = new[]
            {
            new
            {
                productId = product.Id,
                fromLocationId = 1,
                quantity = 1
            }
        }
        };

        // Create two separate sales.
        var firstResponse =
            await client.PostAsJsonAsync("/sales", request);

        var secondResponse =
            await client.PostAsJsonAsync("/sales", request);

        Assert.Equal(HttpStatusCode.Created,
            firstResponse.StatusCode);

        Assert.Equal(HttpStatusCode.Created,
            secondResponse.StatusCode);

        var first = await firstResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var second = await secondResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        string firstNumber = first.GetProperty(
            "receiptNumber").GetString()!;

        string secondNumber = second.GetProperty(
            "receiptNumber").GetString()!;

        // The first sale receives sequence 1.
        Assert.Equal(1,
            first.GetProperty("receiptSequence").GetInt32());

        // Receipt numbers follow the correct date format.
        Assert.Matches(@"^\d{8}-001$", firstNumber);

        // No two receipts have the same number.
        Assert.NotEqual(firstNumber, secondNumber);

        // Verify sequence behavior even if the business
        // date changes between requests at 3 AM.
        string firstDate = first.GetProperty(
            "businessDate").GetString()!;

        string secondDate = second.GetProperty(
            "businessDate").GetString()!;

        int expectedSecondSequence =
            firstDate == secondDate ? 2 : 1;

        Assert.Equal(expectedSecondSequence,
            second.GetProperty("receiptSequence").GetInt32());

        Assert.Matches(
            $@"^\d{{8}}-{expectedSecondSequence:D3}$",
            secondNumber);

        // Verify the historical receipt uses the same number.
        int firstSaleId = first.GetProperty("id").GetInt32();

        var savedReceipt =
            await client.GetFromJsonAsync<JsonElement>(
                $"/sales/{firstSaleId}");

        Assert.Equal(firstNumber,
            savedReceipt.GetProperty(
                "receiptNumber").GetString());
    }

    [Fact]
    public async Task ConcurrentSales_DoNotCreateDuplicateReceipts()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        // 1. Create a product.
        var productResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Concurrent Receipt Test",
                category = "Champagne",
                purchaseCost = 75m,
                sellingPrice = 180m
            });

        Assert.Equal(
            HttpStatusCode.Created,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(product);

        // 2. Receive enough stock for both sales.
        var stockResponse = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = product.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)1,
                quantity = 10
            });

        Assert.Equal(
            HttpStatusCode.Created,
            stockResponse.StatusCode);

        // 3. Prepare an identical request for both sales.
        var request = new
        {
            items = new[]
            {
            new
            {
                productId = product.Id,
                fromLocationId = 1,
                quantity = 1
            }
        }
        };

        // 4. Submit both requests concurrently.
        var tasks = Enumerable.Range(0, 2)
            .Select(_ => client.PostAsJsonAsync("/sales", request));

        var responses = await Task.WhenAll(tasks);

        try
        {
            // 5. Each request must succeed or report
            // a temporary SQLite busy condition.
            Assert.All(responses, response =>
                Assert.True(
                    response.StatusCode == HttpStatusCode.Created ||
                    response.StatusCode == HttpStatusCode.ServiceUnavailable,
                    $"Unexpected status: {response.StatusCode}"
                ));

            int successfulSales = responses.Count(response =>
                response.StatusCode == HttpStatusCode.Created);

            Assert.True(successfulSales >= 1,
                "At least one concurrent sale should succeed.");

            // 6. Read the sales actually committed to SQLite.
            using var scope = factory.Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

            var storedSales = await db.Sales
                .AsNoTracking()
                .ToListAsync();

            Assert.Equal(successfulSales, storedSales.Count);

            // 7. Every committed sale must have receipt data.
            Assert.All(storedSales, sale =>
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(sale.ReceiptNumber));

                Assert.NotNull(sale.ReceiptSequence);
            });

            // 8. Full receipt numbers must be unique.
            Assert.Equal(
                storedSales.Count,
                storedSales
                    .Select(s => s.ReceiptNumber)
                    .Distinct()
                    .Count());

            // 9. Successful receipts on each business date
            // must have consecutive sequences starting at 1.
            foreach (var group in storedSales
                .GroupBy(s => s.BusinessDate))
            {
                var sequences = group
                    .Select(s => s.ReceiptSequence!.Value)
                    .OrderBy(n => n)
                    .ToArray();

                var expected = Enumerable.Range(
                    1, sequences.Length);

                Assert.True(
                    sequences.SequenceEqual(expected),
                    "Committed receipt sequences contain a gap.");
            }
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task TwoLadySale_AllocatesAmountsAndSavesHistory()
    {
        // Arrange: isolated test database.
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        // 1. Create Champagne.
        var champagneResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Allocation Champagne",
                category = "Champagne",
                purchaseCost = 75m,
                sellingPrice = 180m
            });

        Assert.Equal(HttpStatusCode.Created,
            champagneResponse.StatusCode);

        var champagne = await champagneResponse.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(champagne);

        // 2. Create Wine.
        var wineResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Allocation Wine",
                category = "Wine",
                purchaseCost = 30m,
                sellingPrice = 80m
            });

        Assert.Equal(HttpStatusCode.Created,
            wineResponse.StatusCode);

        var wine = await wineResponse.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(wine);

        // 3. Receive inventory.
        var champagneStockResponse = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = champagne.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)1,
                quantity = 5
            });

        var wineStockResponse = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = wine.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)4,
                quantity = 3
            });

        Assert.Equal(HttpStatusCode.Created,
            champagneStockResponse.StatusCode);

        Assert.Equal(HttpStatusCode.Created,
            wineStockResponse.StatusCode);

        // 4. Create a sale allocated to two ladies.
        var saleResponse = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                excludedAmount = 40m,
                ladyNames = new[] { "Hana", "Yuna" },

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

        Assert.Equal(HttpStatusCode.Created,
            saleResponse.StatusCode);

        var sale = await saleResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        // 5. Verify original receipt totals.
        Assert.Equal(440m,
            sale.GetProperty("totalRevenue").GetDecimal());

        Assert.Equal(180m,
            sale.GetProperty("totalCost").GetDecimal());

        Assert.Equal(260m,
            sale.GetProperty("grossProfit").GetDecimal());

        Assert.Equal(31.50m,
            sale.GetProperty("totalBack").GetDecimal());

        Assert.Equal(40m,
            sale.GetProperty("excludedAmount").GetDecimal());

        Assert.Equal(400m,
            sale.GetProperty("eligibleSales").GetDecimal());

        // 6. Verify two allocation records in POST response.
        var allocations = sale.GetProperty("allocations")
            .EnumerateArray()
            .ToList();

        Assert.Equal(2, allocations.Count);

        Assert.Equal("Hana",
            allocations[0]
                .GetProperty("ladyNameSnapshot").GetString());

        Assert.Equal("Yuna",
            allocations[1]
                .GetProperty("ladyNameSnapshot").GetString());

        foreach (var allocation in allocations)
        {
            Assert.Equal(200m,
                allocation.GetProperty(
                    "allocatedEligibleSales").GetDecimal());

            Assert.Equal(90m,
                allocation.GetProperty(
                    "allocatedBottleCost").GetDecimal());

            Assert.Equal(110m,
                allocation.GetProperty("netSales").GetDecimal());

            Assert.Equal(15.75m,
                allocation.GetProperty(
                    "allocatedBack").GetDecimal());
        }

        // 7. Verify that allocated totals reconcile.
        Assert.Equal(400m,
            allocations.Sum(a => a.GetProperty(
                "allocatedEligibleSales").GetDecimal()));

        Assert.Equal(180m,
            allocations.Sum(a => a.GetProperty(
                "allocatedBottleCost").GetDecimal()));

        Assert.Equal(31.50m,
            allocations.Sum(a => a.GetProperty(
                "allocatedBack").GetDecimal()));

        // 8. Confirm allocations were saved in SQLite.
        int saleId = sale.GetProperty("id").GetInt32();

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        var storedAllocations = await db.SaleAllocations
            .AsNoTracking()
            .Where(a => a.SaleId == saleId)
            .OrderBy(a => a.Position)
            .ToListAsync();

        Assert.Equal(2, storedAllocations.Count);

        Assert.Equal("Hana",
            storedAllocations[0].LadyNameSnapshot);

        Assert.Equal("Yuna",
            storedAllocations[1].LadyNameSnapshot);

        Assert.Equal(200m,
            storedAllocations[0].AllocatedEligibleSales);

        Assert.Equal(15.75m,
            storedAllocations[0].AllocatedBack);

        // 9. Retrieve the historical receipt.
        var historicalResponse = await client.GetAsync(
            $"/sales/{saleId}");

        Assert.Equal(HttpStatusCode.OK,
            historicalResponse.StatusCode);

        var historicalReceipt = await historicalResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(400m,
            historicalReceipt.GetProperty(
                "eligibleSales").GetDecimal());

        Assert.Equal(31.50m,
            historicalReceipt.GetProperty(
                "totalBack").GetDecimal());

        var historicalAllocations = historicalReceipt
            .GetProperty("allocations")
            .EnumerateArray()
            .ToList();

        Assert.Equal(2, historicalAllocations.Count);

        Assert.Equal("Hana",
            historicalAllocations[0]
                .GetProperty("ladyNameSnapshot").GetString());

        Assert.Equal(15.75m,
            historicalAllocations[0]
                .GetProperty("allocatedBack").GetDecimal());

        // 10. Inventory must only be deducted once,
        // regardless of the number of ladies.
        var champagneStock =
            await client.GetFromJsonAsync<JsonElement>(
                $"/products/{champagne.Id}/stock");

        var wineStock =
            await client.GetFromJsonAsync<JsonElement>(
                $"/products/{wine.Id}/stock");

        Assert.Equal(3,
            champagneStock.GetProperty("totalStock").GetInt32());

        Assert.Equal(2,
            wineStock.GetProperty("totalStock").GetInt32());
    }

    [Fact]
    public async Task EightLadySale_PreservesRoundingTotals()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        // 1. Create a Champagne product.
        var productResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Rounding Test Champagne",
                category = "Champagne",
                purchaseCost = 30m,
                sellingPrice = 100.01m
            });

        Assert.Equal(HttpStatusCode.Created,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(product);

        // 2. Receive one bottle into Store.
        var stockResponse = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = product.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)1,
                quantity = 1
            });

        Assert.Equal(HttpStatusCode.Created,
            stockResponse.StatusCode);

        // 3. Prepare eight unique lady names.
        var ladyNames = Enumerable.Range(1, 8)
            .Select(i => $"Lady {i}")
            .ToArray();

        // 4. Sell one bottle, allocated to all eight ladies.
        var saleResponse = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                excludedAmount = 0m,
                ladyNames,
                items = new[]
                {
                new
                {
                    productId = product.Id,
                    fromLocationId = 1,
                    quantity = 1
                }
                }
            });

        Assert.Equal(HttpStatusCode.Created,
            saleResponse.StatusCode);

        var sale = await saleResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        // 5. Verify original receipt totals.
        Assert.Equal(100.01m,
            sale.GetProperty("totalRevenue").GetDecimal());

        Assert.Equal(30m,
            sale.GetProperty("totalCost").GetDecimal());

        Assert.Equal(70.01m,
            sale.GetProperty("grossProfit").GetDecimal());

        Assert.Equal(10.5015m,
            sale.GetProperty("totalBack").GetDecimal());

        // 6. Verify exactly eight allocations.
        var allocations = sale.GetProperty("allocations")
            .EnumerateArray()
            .ToList();

        Assert.Equal(8, allocations.Count);

        // 7. Verify the first and last allocations.
        Assert.Equal("Lady 1",
            allocations[0]
                .GetProperty("ladyNameSnapshot").GetString());

        Assert.Equal("Lady 8",
            allocations[7]
                .GetProperty("ladyNameSnapshot").GetString());

        // 8. Verify rounding.
        // First lady receives the extra cent.
        Assert.Equal(12.51m,
            allocations[0]
                .GetProperty("allocatedEligibleSales")
                .GetDecimal());

        // Remaining seven ladies receive 12.50 each.
        foreach (var allocation in allocations.Skip(1))
        {
            Assert.Equal(12.50m,
                allocation.GetProperty("allocatedEligibleSales")
                    .GetDecimal());
        }

        // Bottle cost divides equally.
        foreach (var allocation in allocations)
        {
            Assert.Equal(3.75m,
                allocation.GetProperty("allocatedBottleCost")
                    .GetDecimal());
        }

        // BACK has a four-decimal rounding remainder.
        foreach (var allocation in allocations.Take(7))
        {
            Assert.Equal(1.3127m,
                allocation.GetProperty("allocatedBack")
                    .GetDecimal());
        }

        Assert.Equal(1.3126m,
            allocations[7]
                .GetProperty("allocatedBack").GetDecimal());

        // 9. Most importantly: allocations must reconcile
        // exactly to the original receipt totals.
        Assert.Equal(100.01m,
            allocations.Sum(a => a.GetProperty(
                "allocatedEligibleSales").GetDecimal()));

        Assert.Equal(30m,
            allocations.Sum(a => a.GetProperty(
                "allocatedBottleCost").GetDecimal()));

        Assert.Equal(10.5015m,
            allocations.Sum(a => a.GetProperty(
                "allocatedBack").GetDecimal()));

        // 10. Verify all eight allocations were saved.
        int saleId = sale.GetProperty("id").GetInt32();

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        Assert.Equal(8,
            await db.SaleAllocations
                .CountAsync(a => a.SaleId == saleId));

        // 11. Verify the historical receipt.
        var historicalReceipt =
            await client.GetFromJsonAsync<JsonElement>(
                $"/sales/{saleId}");

        Assert.Equal(8,
            historicalReceipt.GetProperty("allocations")
                .GetArrayLength());

        Assert.Equal(10.5015m,
            historicalReceipt.GetProperty("totalBack")
                .GetDecimal());

        // 12. Inventory was deducted only once.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{product.Id}/stock");

        Assert.Equal(0,
            stock.GetProperty("totalStock").GetInt32());
    }

    [Fact]
    public async Task InvalidLadyLists_RejectWithoutCreatingSales()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        var items = new[]
        {
        new
        {
            productId = 1,
            fromLocationId = 1,
            quantity = 1
        }
    };

        // 1. Nine ladies must be rejected.
        var nineLadies = Enumerable.Range(1, 9)
            .Select(i => $"Lady {i}")
            .ToArray();

        var nineResponse = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                ladyNames = nineLadies,
                items
            });

        Assert.Equal(HttpStatusCode.BadRequest,
            nineResponse.StatusCode);

        // 2. Duplicate lady names must be rejected,
        // even when they use different capitalization.
        var duplicateResponse = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                ladyNames = new[] { "Hana", "hana" },
                items
            });

        Assert.Equal(HttpStatusCode.BadRequest,
            duplicateResponse.StatusCode);

        // 3. Neither request should create database records.
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        Assert.Empty(await db.Sales.ToListAsync());
        Assert.Empty(await db.SaleItems.ToListAsync());
        Assert.Empty(await db.SaleAllocations.ToListAsync());
    }

    [Fact]
    public async Task SaleWithoutLadies_CreatesReceiptWithoutAllocations()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        // 1. Create Champagne.
        var productResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Unallocated Champagne",
                category = "Champagne",
                purchaseCost = 75m,
                sellingPrice = 180m
            });

        Assert.Equal(HttpStatusCode.Created,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(product);

        // 2. Receive two bottles.
        var stockResponse = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = product.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)1,
                quantity = 2
            });

        Assert.Equal(HttpStatusCode.Created,
            stockResponse.StatusCode);

        // 3. Create a sale WITHOUT ladyNames.
        var saleResponse = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                items = new[]
                {
                new
                {
                    productId = product.Id,
                    fromLocationId = 1,
                    quantity = 1
                }
                }
            });

        Assert.Equal(HttpStatusCode.Created,
            saleResponse.StatusCode);

        var sale = await saleResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        // 4. Financial totals remain correct.
        Assert.Equal(180m,
            sale.GetProperty("totalRevenue").GetDecimal());

        Assert.Equal(180m,
            sale.GetProperty("eligibleSales").GetDecimal());

        Assert.Equal(15.75m,
            sale.GetProperty("totalBack").GetDecimal());

        // 5. No ladies means no allocation records.
        Assert.Equal(0,
            sale.GetProperty("allocations").GetArrayLength());

        int saleId = sale.GetProperty("id").GetInt32();

        // 6. Check the historical receipt.
        var historicalReceipt =
            await client.GetFromJsonAsync<JsonElement>(
                $"/sales/{saleId}");

        Assert.Equal(0,
            historicalReceipt.GetProperty("allocations")
                .GetArrayLength());

        // 7. Verify SQLite contains no allocations.
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        Assert.Empty(await db.SaleAllocations.ToListAsync());

        // 8. Inventory was still deducted.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{product.Id}/stock");

        Assert.Equal(1,
            stock.GetProperty("totalStock").GetInt32());
    }
    [Fact]
    public async Task ExcludedAmountAboveRevenue_RollsBackEntireSale()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        // 1. Create product.
        var productResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Excluded Amount Test",
                category = "Champagne",
                purchaseCost = 30m,
                sellingPrice = 80m
            });

        Assert.Equal(HttpStatusCode.Created,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(product);

        // 2. Receive inventory.
        var stockResponse = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = product.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)1,
                quantity = 2
            });

        Assert.Equal(HttpStatusCode.Created,
            stockResponse.StatusCode);

        // 3. Receipt revenue is $80,
        // but excluded amount is $81.
        var failedResponse = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                excludedAmount = 81m,
                ladyNames = new[] { "Hana" },
                items = new[]
                {
                new
                {
                    productId = product.Id,
                    fromLocationId = 1,
                    quantity = 1
                }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest,
            failedResponse.StatusCode);

        // 4. Check that the transaction rolled back.
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        Assert.Empty(await db.Sales.ToListAsync());
        Assert.Empty(await db.SaleItems.ToListAsync());
        Assert.Empty(await db.SaleAllocations.ToListAsync());

        // Only the original inventory receipt exists.
        Assert.Single(await db.StockMovements.ToListAsync());

        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{product.Id}/stock");

        Assert.Equal(2,
            stock.GetProperty("totalStock").GetInt32());

        // 5. A valid sale should still get receipt sequence 1.
        var validResponse = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                excludedAmount = 0m,
                ladyNames = new[] { "Hana" },
                items = new[]
                {
                new
                {
                    productId = product.Id,
                    fromLocationId = 1,
                    quantity = 1
                }
                }
            });

        Assert.Equal(HttpStatusCode.Created,
            validResponse.StatusCode);

        var validSale = await validResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1,
            validSale.GetProperty("receiptSequence").GetInt32());
    }

    [Fact]
    public async Task InvalidAllocationPrecision_RollsBackEntireSale()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        // 1. Create a product with an unsupported price precision.
        var productResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Precision Test Champagne",
                category = "Champagne",
                purchaseCost = 75m,
                sellingPrice = 180.001m
            });

        Assert.Equal(HttpStatusCode.Created,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(product);

        // 2. Receive stock.
        var stockResponse = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = product.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)1,
                quantity = 1
            });

        Assert.Equal(HttpStatusCode.Created,
            stockResponse.StatusCode);

        // 3. Attempt a two-lady allocation.
        var failedResponse = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                excludedAmount = 0m,
                ladyNames = new[] { "Hana", "Yuna" },
                items = new[]
                {
                new
                {
                    productId = product.Id,
                    fromLocationId = 1,
                    quantity = 1
                }
                }
            });

        // The endpoint must reject unsupported precision.
        Assert.Equal(HttpStatusCode.BadRequest,
            failedResponse.StatusCode);

        // 4. Verify no partial sale was stored.
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        Assert.Empty(await db.Sales.ToListAsync());
        Assert.Empty(await db.SaleItems.ToListAsync());
        Assert.Empty(await db.SaleAllocations.ToListAsync());

        // The original inventory receipt remains.
        Assert.Single(await db.StockMovements.ToListAsync());

        // 5. Stock remains untouched.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{product.Id}/stock");

        Assert.Equal(1,
            stock.GetProperty("totalStock").GetInt32());
    }
}