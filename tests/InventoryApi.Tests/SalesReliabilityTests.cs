using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryApi.Data;
using InventoryApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InventoryApi.Tests;

public class SalesReliabilityTests
{
    // Creates a new product in the test database.
    private static async Task<Product> CreateProductAsync(
        HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Sales Reliability Champagne",
                category = "Champagne",
                purchaseCost = 75m,
                sellingPrice = 180m
            });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var product = await response.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(product);

        return product;
    }

    // Receives stock into Store (location 1).
    private static async Task ReceiveStockAsync(
        HttpClient client,
        int productId,
        int quantity)
    {
        using var response = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId,
                fromLocationId = (int?)null,
                toLocationId = (int?)1,
                quantity,
                reference = "RELIABILITY-RECEIPT"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);
    }

    // Add the three test methods below this line.
    [Fact]
    public async Task ConcurrentSales_OnlyOneCanBuyLastBottle()
    {
        // ARRANGE
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        Product product = await CreateProductAsync(client);

        // Only one bottle is available.
        await ReceiveStockAsync(client, product.Id, 1);

        var startSignal = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<HttpStatusCode> MakeSaleAsync()
        {
            await startSignal.Task;

            using var response = await client.PostAsJsonAsync(
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

            return response.StatusCode;
        }

        // ACT: Prepare and release both sales together.
        Task<HttpStatusCode> first = MakeSaleAsync();
        Task<HttpStatusCode> second = MakeSaleAsync();

        startSignal.SetResult(true);

        HttpStatusCode[] statuses = await Task.WhenAll(
            first, second);

        // ASSERT: One success, one rejection.
        Assert.Single(statuses,
            status => status == HttpStatusCode.Created);

        Assert.Single(statuses,
            status => status == HttpStatusCode.BadRequest);

        // Only one sale and one sale item exist.
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        Assert.Equal(1, await db.Sales.CountAsync());
        Assert.Equal(1, await db.SaleItems.CountAsync());

        // One receipt + one successful sale issue.
        Assert.Equal(
            2,
            await db.StockMovements.CountAsync(m =>
                m.ProductId == product.Id));

        // Final inventory cannot be negative.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{product.Id}/stock");

        Assert.Equal(
            0,
            stock.GetProperty("totalStock").GetInt32());

        Assert.Equal(
            0,
            stock.GetProperty("usableStock").GetInt32());
    }

    [Fact]
    public async Task SaleItemWriteFailure_RollsBackEntireSale()
    {
        // ARRANGE
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        Product product = await CreateProductAsync(client);

        // Start with 5 bottles in Store.
        await ReceiveStockAsync(client, product.Id, 5);

        // Create a trigger in the TEMPORARY test database.
        // It forces SaleItem insertion to fail.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

            await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER "FailSaleItemInsert"
            BEFORE INSERT ON "SaleItems"
            BEGIN
                SELECT RAISE(
                    ABORT,
                    'Simulated sale item write failure'
                );
            END;
            """);
        }

        // ACT: Attempt to sell 2 bottles.
        // The sale and movement inserts happen first.
        // The SaleItem insert will fail.
        using var response = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                items = new[]
                {
                new
                {
                    productId = product.Id,
                    fromLocationId = 1,
                    quantity = 2
                }
                }
            });

        // The application currently returns an unhandled
        // server error for this deliberately injected failure.
        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);

        // ASSERT: Verify what actually remained in SQLite.
        using var verificationScope =
            factory.Services.CreateScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        // The sale must not exist.
        Assert.Empty(await verificationDb.Sales
            .AsNoTracking()
            .ToListAsync());

        // No sale item may exist.
        Assert.Empty(await verificationDb.SaleItems
            .AsNoTracking()
            .ToListAsync());

        // Only the original stock receipt remains.
        var movements = await verificationDb.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == product.Id)
            .ToListAsync();

        Assert.Single(movements);
        Assert.Equal(5, movements[0].Quantity);
        Assert.Null(movements[0].FromLocationId);

        // Inventory must remain unchanged.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{product.Id}/stock");

        Assert.Equal(
            5,
            stock.GetProperty("totalStock").GetInt32());
    }
    [Fact]
    public async Task ProductPriceChange_DoesNotAlterPastSale()
    {
        // ARRANGE
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        Product product = await CreateProductAsync(client);

        // Receive 3 bottles into Store.
        await ReceiveStockAsync(client, product.Id, 3);

        // Create a sale of 2 bottles at the original price.
        using var saleResponse = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                items = new[]
                {
                new
                {
                    productId = product.Id,
                    fromLocationId = 1,
                    quantity = 2
                }
                }
            });

        Assert.Equal(
            HttpStatusCode.Created,
            saleResponse.StatusCode);

        var createdSale = await saleResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        int saleId = createdSale
            .GetProperty("id")
            .GetInt32();

        // ACT: Change the product's current values.
        using var updateResponse = await client.PutAsJsonAsync(
            $"/products/{product.Id}",
            new
            {
                name = "Renamed Premium Champagne",
                category = "Sparkling Wine",
                purchaseCost = 100m,
                sellingPrice = 250m
            });

        Assert.Equal(
            HttpStatusCode.NoContent,
            updateResponse.StatusCode);

        // Verify the product really changed.
        var updatedProduct = await client.GetFromJsonAsync<Product>(
            $"/products/{product.Id}");

        Assert.NotNull(updatedProduct);

        Assert.Equal(250m, updatedProduct.SellingPrice);
        Assert.Equal(100m, updatedProduct.PurchaseCost);

        // Retrieve the HISTORICAL sale.
        var historicalSale = await client
            .GetFromJsonAsync<JsonElement>(
                $"/sales/{saleId}");

        // ASSERT: Historical totals remain unchanged.
        Assert.Equal(
            360m,
            historicalSale.GetProperty("totalRevenue").GetDecimal());

        Assert.Equal(
            150m,
            historicalSale.GetProperty("totalCost").GetDecimal());

        Assert.Equal(
            210m,
            historicalSale.GetProperty("grossProfit").GetDecimal());

        // Historical item details also remain unchanged.
        var items = historicalSale
            .GetProperty("items")
            .EnumerateArray()
            .ToList();

        Assert.Single(items);

        var item = items[0];

        Assert.Equal(
            "Sales Reliability Champagne",
            item.GetProperty("productNameSnapshot").GetString());

        Assert.Equal(
            "Champagne",
            item.GetProperty("categorySnapshot").GetString());

        Assert.Equal(
            180m,
            item.GetProperty("unitSellingPrice").GetDecimal());

        Assert.Equal(
            75m,
            item.GetProperty("unitPurchaseCost").GetDecimal());

        Assert.Equal(
            2,
            item.GetProperty("quantity").GetInt32());
    }

    private static async Task<(int SaleId, int MovementId)>
    CreateSingleItemSaleAsync(
        HttpClient client,
        int productId)
    {
        using var response = await client.PostAsJsonAsync(
            "/sales",
            new
            {
                items = new[]
                {
                new
                {
                    productId,
                    fromLocationId = 1,
                    quantity = 1
                }
                }
            });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var sale = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        int saleId = sale
            .GetProperty("id")
            .GetInt32();

        var item = sale
            .GetProperty("items")
            .EnumerateArray()
            .Single();

        int movementId = item
            .GetProperty("stockMovementId")
            .GetInt32();

        return (saleId, movementId);
    }

    [Fact]
    public async Task SaleItem_ReferencesMatchingStockMovement()
    {
        // Arrange
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        Product product = await CreateProductAsync(client);

        await ReceiveStockAsync(client, product.Id, 3);

        // Act
        var (saleId, movementId) =
            await CreateSingleItemSaleAsync(client, product.Id);

        // Read the saved database records.
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        var item = await db.SaleItems
            .AsNoTracking()
            .SingleAsync(i => i.SaleId == saleId);

        var movement = await db.StockMovements
            .AsNoTracking()
            .SingleAsync(m => m.Id == movementId);

        // Assert: Correct relationship.
        Assert.Equal(
            (int?)movementId,
            item.StockMovementId);

        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(product.Id, movement.ProductId);

        // Assert: Correct inventory deduction.
        Assert.Equal((int?)1, movement.FromLocationId);
        Assert.Null(movement.ToLocationId);
        Assert.Equal(1, movement.Quantity);

        Assert.Equal(
            $"SALE-{saleId}-ITEM-1",
            movement.Reference);

        // Assert: Historical price snapshots.
        Assert.Equal(180m, item.UnitSellingPrice);
        Assert.Equal(75m, item.UnitPurchaseCost);
        Assert.Equal("Sales Reliability Champagne",
            item.ProductNameSnapshot);
    }

    [Fact]
    public async Task SaleLinkedMovement_CannotBeReversedOrDeleted()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        Product product = await CreateProductAsync(client);

        // Begin with 3 bottles.
        await ReceiveStockAsync(client, product.Id, 3);

        // Sell one bottle.
        var (saleId, movementId) =
            await CreateSingleItemSaleAsync(client, product.Id);

        // Attempt to reverse the sale's stock deduction.
        using var reverseResponse =
            await client.PostAsJsonAsync(
                $"/stock-movements/{movementId}/reverse",
                new { reason = "Attempt direct reversal" });

        Assert.Equal(
            HttpStatusCode.Conflict,
            reverseResponse.StatusCode);

        // No DELETE endpoint should exist for movements.
        using var deleteResponse = await client.DeleteAsync(
            $"/stock-movements/{movementId}");

        Assert.Equal(
            HttpStatusCode.MethodNotAllowed,
            deleteResponse.StatusCode);

        // Verify the original records remain.
        var history = await client.GetFromJsonAsync<
            List<StockMovement>>(
            $"/stock-movements?productId={product.Id}");

        Assert.NotNull(history);

        // One receipt and one sale issue.
        Assert.Equal(2, history.Count);

        Assert.Contains(history,
            movement => movement.Id == movementId);

        Assert.DoesNotContain(history,
            movement => movement.ReversesMovementId == movementId);

        // The stock deduction must remain effective.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{product.Id}/stock");

        Assert.Equal(
            2,
            stock.GetProperty("totalStock").GetInt32());

        // The sale must still be retrievable.
        using var saleResponse = await client.GetAsync(
            $"/sales/{saleId}");

        Assert.Equal(
            HttpStatusCode.OK,
            saleResponse.StatusCode);
    }

    [Fact]
    public async Task ProductWithRecordedSale_CannotBeDeleted()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        Product product = await CreateProductAsync(client);

        await ReceiveStockAsync(client, product.Id, 3);

        var (saleId, movementId) =
            await CreateSingleItemSaleAsync(client, product.Id);

        // Attempt to delete the product.
        using var deleteResponse = await client.DeleteAsync(
            $"/products/{product.Id}");

        Assert.Equal(
            HttpStatusCode.Conflict,
            deleteResponse.StatusCode);

        // The product must still exist.
        using var productResponse = await client.GetAsync(
            $"/products/{product.Id}");

        Assert.Equal(
            HttpStatusCode.OK,
            productResponse.StatusCode);

        // The original sale must still exist.
        using var saleResponse = await client.GetAsync(
            $"/sales/{saleId}");

        Assert.Equal(
            HttpStatusCode.OK,
            saleResponse.StatusCode);

        // Verify the database relationships remain intact.
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        Assert.True(
            await db.Sales.AnyAsync(s => s.Id == saleId));

        Assert.True(
            await db.SaleItems.AnyAsync(i =>
                i.SaleId == saleId &&
                i.StockMovementId == movementId));

        Assert.True(
            await db.StockMovements.AnyAsync(m =>
                m.Id == movementId));

        // Inventory must still reflect the completed sale.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{product.Id}/stock");

        Assert.Equal(
            2,
            stock.GetProperty("totalStock").GetInt32());
    }
}