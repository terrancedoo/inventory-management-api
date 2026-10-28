using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryApi.Models;
using Xunit;
using InventoryApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryApi.Tests;

public class InventoryEndpointsTests
{
    [Fact]
    public async Task GetLocations_ReturnsFourSeededLocations()
    {
        // Arrange: Start our test API and database.
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        // Act: Send a real HTTP GET request.
        var response = await client.GetAsync("/locations");

        var locations = await response.Content
            .ReadFromJsonAsync<List<StockLocation>>();

        // Assert: Check the results.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(locations);
        Assert.Equal(4, locations.Count);

        Assert.Equal("Store", locations[0].Name);
        Assert.Equal("Locker", locations[1].Name);
        Assert.Equal("Office", locations[2].Name);
        Assert.Equal("Wine Chiller", locations[3].Name);

        Assert.True(locations[0].IsUsable);
        Assert.False(locations[1].IsUsable);
        Assert.False(locations[2].IsUsable);
        Assert.True(locations[3].IsUsable);
    }

    [Fact]
    public async Task ReceiveAndTransfer_UpdatesStockBalances()
    {
        // Arrange
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        // Create a test product.
        var createResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Automated Test Champagne",
                category = "Champagne",
                purchaseCost = 75m,
                sellingPrice = 180m
            });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var product = await createResponse.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(product);

        // Act 1: Receive 10 units into Store.
        var receiptResponse = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = product.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)1,
                quantity = 10,
                reference = "AUTO-RECEIPT-001"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            receiptResponse.StatusCode);

        // Act 2: Transfer 3 units from Store to Locker.
        var transferResponse = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = product.Id,
                fromLocationId = (int?)1,
                toLocationId = (int?)2,
                quantity = 3,
                reference = "AUTO-TRANSFER-001"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            transferResponse.StatusCode);

        // Act 3: Retrieve the updated stock.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{product.Id}/stock");

        // Assert
        Assert.Equal(
            10,
            stock.GetProperty("totalStock").GetInt32());

        Assert.Equal(
            7,
            stock.GetProperty("usableStock").GetInt32());

        var locations = stock.GetProperty("locations")
            .EnumerateArray()
            .ToList();

        int storeQuantity = locations
            .Single(l =>
                l.GetProperty("locationId").GetInt32() == 1)
            .GetProperty("quantity")
            .GetInt32();

        int lockerQuantity = locations
            .Single(l =>
                l.GetProperty("locationId").GetInt32() == 2)
            .GetProperty("quantity")
            .GetInt32();

        Assert.Equal(7, storeQuantity);
        Assert.Equal(3, lockerQuantity);
    }

    [Fact]
    public async Task IssueWithoutStock_IsRejectedAndNotSaved()
    {
        // Arrange
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        var createResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Empty Stock Test Product",
                category = "Wine",
                purchaseCost = 20m,
                sellingPrice = 50m
            });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var product = await createResponse.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(product);

        // Act: Try to issue 5 units from an empty Store.
        var issueResponse = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = product.Id,
                fromLocationId = (int?)1,
                toLocationId = (int?)null,
                quantity = 5,
                reference = "AUTO-INVALID-001"
            });

        // Assert 1: API rejects the movement.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            issueResponse.StatusCode);

        // Assert 2: Stock remains zero.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{product.Id}/stock");

        Assert.Equal(
            0,
            stock.GetProperty("totalStock").GetInt32());

        // Assert 3: No invalid movement was saved.
        var movements = await client.GetFromJsonAsync<
            List<StockMovement>>(
            $"/stock-movements?productId={product.Id}");

        Assert.NotNull(movements);
        Assert.Empty(movements);
    }

    [Fact]
    public async Task TwoSimultaneousIssues_OnlyOneSucceeds()
    {
        // ARRANGE: Create an isolated API and database.
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        // Create a product.
        using var productResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Concurrency Test Champagne",
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

        // Receive exactly ONE unit into Store.
        using var receiptResponse = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = product.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)1,
                quantity = 1,
                reference = "CONCURRENT-RECEIPT"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            receiptResponse.StatusCode);

        // Both requests will wait for the same start signal.
        var startSignal = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<HttpStatusCode> IssueOneAsync(string reference)
        {
            await startSignal.Task;

            using var response = await client.PostAsJsonAsync(
                "/stock-movements",
                new
                {
                    productId = product.Id,
                    fromLocationId = (int?)1,
                    toLocationId = (int?)null,
                    quantity = 1,
                    reference
                });

            return response.StatusCode;
        }

        // ACT: Prepare two requests.
        Task<HttpStatusCode> first =
            IssueOneAsync("CONCURRENT-ISSUE-A");

        Task<HttpStatusCode> second =
            IssueOneAsync("CONCURRENT-ISSUE-B");

        // Release both requests together.
        startSignal.SetResult(true);

        HttpStatusCode[] statuses = await Task.WhenAll(
            first,
            second);

        // ASSERT 1: Exactly one request succeeds.
        Assert.Single(statuses,
            status => status == HttpStatusCode.Created);

        // ASSERT 2: Exactly one request is rejected.
        Assert.Single(statuses,
            status => status == HttpStatusCode.BadRequest);

        // ASSERT 3: Final stock is zero, not negative.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{product.Id}/stock");

        Assert.Equal(
            0,
            stock.GetProperty("totalStock").GetInt32());

        Assert.Equal(
            0,
            stock.GetProperty("usableStock").GetInt32());

        // ASSERT 4: Only one issue is in movement history.
        var movements = await client.GetFromJsonAsync<
            List<StockMovement>>(
            $"/stock-movements?productId={product.Id}");

        Assert.NotNull(movements);

        // One receipt + one successful issue = 2 records.
        Assert.Equal(2, movements.Count);

        int issueCount = movements.Count(m =>
            m.FromLocationId == 1 &&
            m.ToLocationId is null);

        Assert.Equal(1, issueCount);
    }

    [Fact]
    public async Task HeldTransaction_CompetingIssueSeesUpdatedStock()
    {
        // ARRANGE: Create an isolated test database.
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        using var productResponse = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Transaction Lock Test",
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

        // Receive one bottle.
        using var receiptResponse = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId = product.Id,
                fromLocationId = (int?)null,
                toLocationId = (int?)1,
                quantity = 1,
                reference = "LOCK-RECEIPT"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            receiptResponse.StatusCode);

        // Obtain a separate DbContext.
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        // Hold a write transaction open.
        await using var heldTransaction =
            await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

        // Start a competing HTTP issue request.
        Task<HttpResponseMessage> competingRequest =
            client.PostAsJsonAsync(
                "/stock-movements",
                new
                {
                    productId = product.Id,
                    fromLocationId = (int?)1,
                    toLocationId = (int?)null,
                    quantity = 1,
                    reference = "COMPETING-ISSUE"
                });

        // Give the competing request an opportunity to start.
        await Task.Delay(100);

        // Our held transaction should prevent it from finishing.
        Assert.False(competingRequest.IsCompleted);

        // Consume the only available bottle
        // within the held transaction.
        db.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            FromLocationId = 1,
            ToLocationId = null,
            Quantity = 1,
            Reference = "HELD-TRANSACTION-ISSUE",
            OccurredAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        // Release the database write lock.
        await heldTransaction.CommitAsync();

        // The competing request can now finish.
        using var response = await competingRequest.WaitAsync(
            TimeSpan.FromSeconds(15));

        // It must observe zero available stock.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        // Verify final stock.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{product.Id}/stock");

        Assert.Equal(
            0,
            stock.GetProperty("totalStock").GetInt32());

        // Verify no duplicate issue was saved.
        var movements = await client.GetFromJsonAsync<
            List<StockMovement>>(
            $"/stock-movements?productId={product.Id}");

        Assert.NotNull(movements);
        Assert.Equal(2, movements.Count);
    }
}