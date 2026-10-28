using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryApi.Models;
using Xunit;

namespace InventoryApi.Tests;

public class StockMovementReversalTests
{
    // Helper: create a new product for each test.
    private static async Task<int> CreateProductAsync(
        HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/products",
            new
            {
                name = "Reversal Test Champagne",
                category = "Champagne",
                purchaseCost = 75m,
                sellingPrice = 180m
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var product = await response.Content
            .ReadFromJsonAsync<Product>();

        Assert.NotNull(product);
        return product.Id;
    }

    // Helper: record a valid movement.
    private static async Task<StockMovement> RecordMovementAsync(
        HttpClient client,
        int productId,
        int? fromLocationId,
        int? toLocationId,
        int quantity)
    {
        using var response = await client.PostAsJsonAsync(
            "/stock-movements",
            new
            {
                productId,
                fromLocationId,
                toLocationId,
                quantity
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var movement = await response.Content
            .ReadFromJsonAsync<StockMovement>();

        Assert.NotNull(movement);
        return movement;
    }

    [Fact]
    public async Task ReverseTransfer_RestoresOriginalBalances()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();

        int productId = await CreateProductAsync(client);

        // Receive 10 into Store.
        await RecordMovementAsync(client, productId, null, 1, 10);

        // Transfer 3 to Locker.
        var transfer = await RecordMovementAsync(
            client, productId, 1, 2, 3);

        // Reverse that transfer.
        using var response = await client.PostAsJsonAsync(
            $"/stock-movements/{transfer.Id}/reverse",
            new { reason = "Wrong locker transfer" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var reversal = await response.Content
            .ReadFromJsonAsync<StockMovement>();

        Assert.NotNull(reversal);
        Assert.Equal(transfer.Id, reversal.ReversesMovementId);
        Assert.Equal("Wrong locker transfer", reversal.ReversalReason);
        Assert.Equal(2, reversal.FromLocationId);
        Assert.Equal(1, reversal.ToLocationId);
        Assert.Equal(3, reversal.Quantity);

        // Check the resulting stock.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{productId}/stock");

        Assert.Equal(
            10, stock.GetProperty("totalStock").GetInt32());

        Assert.Equal(
            10, stock.GetProperty("usableStock").GetInt32());

        var locations = stock.GetProperty("locations")
            .EnumerateArray()
            .ToList();

        int store = locations.Single(l =>
            l.GetProperty("locationId").GetInt32() == 1)
            .GetProperty("quantity").GetInt32();

        int locker = locations.Single(l =>
            l.GetProperty("locationId").GetInt32() == 2)
            .GetProperty("quantity").GetInt32();

        Assert.Equal(10, store);
        Assert.Equal(0, locker);

        // Original + receipt + reversal still exist.
        var history = await client.GetFromJsonAsync<
            List<StockMovement>>(
            $"/stock-movements?productId={productId}");

        Assert.NotNull(history);
        Assert.Equal(3, history.Count);
        Assert.Contains(history, m => m.Id == transfer.Id);
        Assert.Contains(history, m => m.Id == reversal.Id);
    }

    [Fact]
    public async Task ReverseReceiptWithoutStock_IsRejected()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();

        int productId = await CreateProductAsync(client);

        // Receive one bottle, then issue it.
        var receipt = await RecordMovementAsync(
            client, productId, null, 1, 1);

        await RecordMovementAsync(
            client, productId, 1, null, 1);

        // Cannot undo receipt: Store now has zero.
        using var response = await client.PostAsJsonAsync(
            $"/stock-movements/{receipt.Id}/reverse",
            new { reason = "Incorrect receipt" });

        Assert.Equal(
            HttpStatusCode.BadRequest, response.StatusCode);

        var history = await client.GetFromJsonAsync<
            List<StockMovement>>(
            $"/stock-movements?productId={productId}");

        Assert.NotNull(history);
        Assert.Equal(2, history.Count);

        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{productId}/stock");

        Assert.Equal(
            0, stock.GetProperty("totalStock").GetInt32());
    }

    [Fact]
    public async Task DuplicateReversal_ReturnsConflict()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();

        int productId = await CreateProductAsync(client);

        var receipt = await RecordMovementAsync(
            client, productId, null, 1, 2);

        // First reversal succeeds.
        using var first = await client.PostAsJsonAsync(
            $"/stock-movements/{receipt.Id}/reverse",
            new { reason = "Duplicate receipt" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var reversal = await first.Content
            .ReadFromJsonAsync<StockMovement>();

        Assert.NotNull(reversal);

        // Reversing the original again is forbidden.
        using var second = await client.PostAsJsonAsync(
            $"/stock-movements/{receipt.Id}/reverse",
            new { reason = "Trying again" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // A reversal itself cannot be reversed.
        using var third = await client.PostAsJsonAsync(
            $"/stock-movements/{reversal.Id}/reverse",
            new { reason = "Undo the reversal" });

        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);

        var history = await client.GetFromJsonAsync<
            List<StockMovement>>(
            $"/stock-movements?productId={productId}");

        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public async Task ReverseIssue_RestoresStock()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();
        await factory.InitializeDatabaseAsync();

        int productId = await CreateProductAsync(client);

        await RecordMovementAsync(
            client, productId, null, 1, 5);

        var issue = await RecordMovementAsync(
            client, productId, 1, null, 2);

        // Undo the issue.
        using var response = await client.PostAsJsonAsync(
            $"/stock-movements/{issue.Id}/reverse",
            new { reason = "Issue entered in error" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{productId}/stock");

        Assert.Equal(
            5, stock.GetProperty("totalStock").GetInt32());

        Assert.Equal(
            5, stock.GetProperty("usableStock").GetInt32());
    }

    [Fact]
    public async Task ReverseNonexistentMovement_ReturnsNotFound()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        using var response = await client.PostAsJsonAsync(
            "/stock-movements/999999/reverse",
            new { reason = "Correct an incorrect movement" });

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task InvalidReversalReasons_DoNotCreateMovements()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        int productId = await CreateProductAsync(client);

        var receipt = await RecordMovementAsync(
            client, productId, null, 1, 2);

        string?[] invalidReasons =
        {
        null,
        "",
        "   ",
        new string('X', 201)
    };

        foreach (string? reason in invalidReasons)
        {
            using var response = await client.PostAsJsonAsync(
                $"/stock-movements/{receipt.Id}/reverse",
                new { reason });

            Assert.Equal(
                HttpStatusCode.BadRequest,
                response.StatusCode);
        }

        // No reversal should have been saved.
        var history = await client.GetFromJsonAsync<
            List<StockMovement>>(
            $"/stock-movements?productId={productId}");

        Assert.NotNull(history);
        Assert.Single(history);

        // Original stock should remain unchanged.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{productId}/stock");

        Assert.Equal(
            2,
            stock.GetProperty("totalStock").GetInt32());
    }

    [Fact]
    public async Task ConcurrentReversals_OnlyOneIsCreated()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        int productId = await CreateProductAsync(client);

        // Receive two bottles into Store.
        var receipt = await RecordMovementAsync(
            client, productId, null, 1, 2);

        // Synchronise the two requests.
        var startSignal = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<HttpStatusCode> ReverseAsync(string reason)
        {
            await startSignal.Task;

            using var response = await client.PostAsJsonAsync(
                $"/stock-movements/{receipt.Id}/reverse",
                new { reason });

            return response.StatusCode;
        }

        // Prepare both reversal attempts.
        Task<HttpStatusCode> first =
            ReverseAsync("Correction A");

        Task<HttpStatusCode> second =
            ReverseAsync("Correction B");

        // Start them together.
        startSignal.SetResult(true);

        HttpStatusCode[] statuses = await Task.WhenAll(
            first, second);

        // Exactly one must succeed.
        Assert.Single(statuses,
            status => status == HttpStatusCode.Created);

        // Exactly one must be rejected.
        Assert.Single(statuses,
            status => status == HttpStatusCode.Conflict);

        // Only the original movement and one reversal exist.
        var history = await client.GetFromJsonAsync<
            List<StockMovement>>(
            $"/stock-movements?productId={productId}");

        Assert.NotNull(history);
        Assert.Equal(2, history.Count);

        Assert.Single(history,
            movement =>
                movement.ReversesMovementId == receipt.Id);

        // The receipt has been fully reversed.
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{productId}/stock");

        Assert.Equal(
            0,
            stock.GetProperty("totalStock").GetInt32());
    }

    [Fact]
    public async Task DeleteProductWithHistory_ReturnsConflict()
    {
        using var factory = new InventoryApiFactory();
        using var client = factory.CreateClient();

        await factory.InitializeDatabaseAsync();

        int productId = await CreateProductAsync(client);

        await RecordMovementAsync(
            client, productId, null, 1, 3);

        // Attempt to delete the product.
        using var deleteResponse = await client.DeleteAsync(
            $"/products/{productId}");

        Assert.Equal(
            HttpStatusCode.Conflict,
            deleteResponse.StatusCode);

        // Confirm the product still exists.
        using var productResponse = await client.GetAsync(
            $"/products/{productId}");

        Assert.Equal(
            HttpStatusCode.OK,
            productResponse.StatusCode);

        // Confirm its history is preserved.
        var history = await client.GetFromJsonAsync<
            List<StockMovement>>(
            $"/stock-movements?productId={productId}");

        Assert.NotNull(history);
        Assert.Single(history);

        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/products/{productId}/stock");

        Assert.Equal(
            3,
            stock.GetProperty("totalStock").GetInt32());
    }
}