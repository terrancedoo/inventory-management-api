using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using InventoryApi.Data;
using InventoryApi.Models;
using InventoryApi.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("InventoryDb")
    ?? throw new InvalidOperationException(
        "InventoryDb connection string is missing.");

builder.Services.AddDbContext<InventoryDbContext>(
    options => options.UseSqlite(connectionString));

var app = builder.Build();

// Endpoint 1: Application information
app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        Name = "Inventory Management API",
        Version = "1.0",
        Description = "My C# Developer Portfolio Project"
    });
});

// Endpoint 2: Health check
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        Status = "Healthy",
        Timestamp = DateTime.UtcNow
    });
});
// Endpoint 3: About
app.MapGet("/about", () =>
{
    return Results.Ok(new
    {
        developer = "Terrance",
        language = "C#",
        project = "Inventory Management System",
    });
});

// Endpoint 4: Get all products
app.MapGet("/products", async (InventoryDbContext db) =>
{
    var products = await db.Products
        .AsNoTracking()
        .OrderBy(p => p.Id)
        .ToListAsync();

    return Results.Ok(products);
});

// Endpoint 5: Get a specific product by ID
app.MapGet("/products/{id:int}",
    async (int id, InventoryDbContext db) =>
{
    var product = await db.Products
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == id);

    if (product is null)
    {
        return Results.NotFound(new
        {
            Message = $"Product with ID {id} was not found."
        });
    }

    return Results.Ok(product);
});

app.MapPost("/products",
    async (CreateProductRequest request, InventoryDbContext db) =>
{
    string name = request.Name?.Trim() ?? "";
    string category = request.Category?.Trim() ?? "";

    // Validate product name
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest(new
        {
            Message = "Product name is required."
        });
    }

    if (name.Length > 120)
    {
        return Results.BadRequest(new
        {
            Message = "Product name cannot exceed 120 characters."
        });
    }

    // Validate category
    if (string.IsNullOrWhiteSpace(category))
    {
        return Results.BadRequest(new
        {
            Message = "Product category is required."
        });
    }

    if (category.Length > 60)
    {
        return Results.BadRequest(new
        {
            Message = "Product category cannot exceed 60 characters."
        });
    }

    // Validate purchase cost
    if (request.PurchaseCost is null ||
        request.PurchaseCost < 0)
    {
        return Results.BadRequest(new
        {
            Message = "Purchase cost must be zero or greater."
        });
    }

    // Validate selling price
    if (request.SellingPrice is null ||
        request.SellingPrice <= 0)
    {
        return Results.BadRequest(new
        {
            Message = "Selling price must be greater than zero."
        });
    }

    // Create new product
    var product = new Product
    {
        Name = name,
        Category = category,
        PurchaseCost = request.PurchaseCost.Value,
        SellingPrice = request.SellingPrice.Value
    };

    // Add product to database
    db.Products.Add(product);



    // Save changes to SQLite
    await db.SaveChangesAsync();

    // Return created product
    return Results.Created($"/products/{product.Id}", product);


});

app.MapPut("/products/{id:int}",
    async (int id, UpdateProductRequest request, InventoryDbContext db) =>
{
    // Step 1: Find the existing product
    var product = await db.Products.FindAsync(id);

    if (product is null)
    {
        return Results.NotFound(new
        {
            Message = $"Product with ID {id} was not found."
        });
    }

    // Step 2: Clean the incoming strings
    string name = request.Name?.Trim() ?? "";
    string category = request.Category?.Trim() ?? "";

    // Step 3: Validate product name
    if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
    {
        return Results.BadRequest(new
        {
            Message = "Product name must contain 1 to 120 characters."
        });
    }

    // Step 4: Validate category
    if (string.IsNullOrWhiteSpace(category) || category.Length > 60)
    {
        return Results.BadRequest(new
        {
            Message = "Category must contain 1 to 60 characters."
        });
    }

    // Step 5: Validate prices
    if (request.PurchaseCost is null || request.PurchaseCost < 0)
    {
        return Results.BadRequest(new
        {
            Message = "Purchase cost must be zero or greater."
        });
    }

    if (request.SellingPrice is null || request.SellingPrice <= 0)
    {
        return Results.BadRequest(new
        {
            Message = "Selling price must be greater than zero."
        });
    }

    // Step 6: Update the existing product
    product.Name = name;
    product.Category = category;
    product.PurchaseCost = request.PurchaseCost.Value;
    product.SellingPrice = request.SellingPrice.Value;

    // Step 7: Save changes to SQLite
    await db.SaveChangesAsync();

    // Step 8: Return success with no response body
    return Results.NoContent();
});

app.MapDelete("/products/{id:int}",
    async (int id, InventoryDbContext db) =>
{
    // 1. Find the product.
    var product = await db.Products.FindAsync(id);

    if (product is null)
    {
        return Results.NotFound(new
        {
            Message = $"Product with ID {id} was not found."
        });
    }

    // 2. Check for stock movement history.
    bool hasMovementHistory = await db.StockMovements
        .AnyAsync(m => m.ProductId == id);

    if (hasMovementHistory)
    {
        return Results.Conflict(new
        {
            Message = "Cannot delete a product with stock movement history."
        });
    }

    // 3. Only delete products without movement history.
    db.Products.Remove(product);

    await db.SaveChangesAsync();

    return Results.NoContent();
});
// Endpoint 6: Get all stock locations
app.MapGet("/locations",
    async (InventoryDbContext db) =>
{
    var locations = await db.StockLocations
        .AsNoTracking()
        .OrderBy(l => l.Id)
        .ToListAsync();

    return Results.Ok(locations);
});

// Endpoint 7: Get stock information for a specific product
app.MapGet("/products/{id:int}/stock",
    async (int id, InventoryDbContext db) =>
{
    // Step 1: Check if product exists
    bool productExists = await db.Products
        .AnyAsync(p => p.Id == id);

    if (!productExists)
    {
        return Results.NotFound(new
        {
            Message = $"Product with ID {id} was not found."
        });
    }

    // Step 2: Get all inventory locations
    var locations = await db.StockLocations
        .AsNoTracking()
        .OrderBy(l => l.Id)
        .ToListAsync();

    // Step 3: Get stock movements for this product
    var movements = await db.StockMovements
        .AsNoTracking()
        .Where(m => m.ProductId == id)
        .ToListAsync();

    // Step 4: Calculate stock at each location
    var balances = locations.Select(location =>
    {
        int incoming = movements
            .Where(m => m.ToLocationId == location.Id)
            .Sum(m => m.Quantity);

        int outgoing = movements
            .Where(m => m.FromLocationId == location.Id)
            .Sum(m => m.Quantity);

        return new
        {
            LocationId = location.Id,
            LocationName = location.Name,
            IsUsable = location.IsUsable,
            Quantity = incoming - outgoing
        };
    }).ToList();

    // Step 5: Calculate total stock
    int totalStock = balances.Sum(b => b.Quantity);

    // Step 6: Calculate usable stock
    int usableStock = balances
        .Where(b => b.IsUsable)
        .Sum(b => b.Quantity);

    // Step 7: Return the result
    return Results.Ok(new
    {
        ProductId = id,
        TotalStock = totalStock,
        UsableStock = usableStock,
        Locations = balances
    });
});

// Endpoint 8: Create a stock movement
app.MapPost("/stock-movements",
    async (CreateStockMovementRequest request,
           InventoryDbContext db) =>
{
    // 1. Validate information that does not require database access.
    if (request.ProductId <= 0)
    {
        return Results.BadRequest(new
        {
            Message = "A valid product ID is required."
        });
    }

    if (request.Quantity <= 0)
    {
        return Results.BadRequest(new
        {
            Message = "Quantity must be greater than zero."
        });
    }

    if (request.FromLocationId is null &&
        request.ToLocationId is null)
    {
        return Results.BadRequest(new
        {
            Message = "Source or destination is required."
        });
    }

    if (request.FromLocationId is not null &&
        request.FromLocationId == request.ToLocationId)
    {
        return Results.BadRequest(new
        {
            Message = "Source and destination cannot be the same."
        });
    }

    string? reference = request.Reference?.Trim();

    if (reference?.Length > 100)
    {
        return Results.BadRequest(new
        {
            Message = "Reference cannot exceed 100 characters."
        });
    }

    try
    {
        // 2. Begin a serializable transaction BEFORE reading stock.
        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        // 3. Check whether the product exists.
        bool productExists = await db.Products
            .AnyAsync(p => p.Id == request.ProductId);

        if (!productExists)
        {
            return Results.BadRequest(new
            {
                Message = "Product does not exist."
            });
        }

        // 4. Validate the source and check its available stock.
        if (request.FromLocationId is int fromId)
        {
            bool sourceExists = await db.StockLocations
                .AnyAsync(l => l.Id == fromId);

            if (!sourceExists)
            {
                return Results.BadRequest(new
                {
                    Message = "Source location does not exist."
                });
            }

            var sourceMovements = await db.StockMovements
                .AsNoTracking()
                .Where(m =>
                    m.ProductId == request.ProductId &&
                    (m.FromLocationId == fromId ||
                     m.ToLocationId == fromId))
                .ToListAsync();

            int incoming = sourceMovements
                .Where(m => m.ToLocationId == fromId)
                .Sum(m => m.Quantity);

            int outgoing = sourceMovements
                .Where(m => m.FromLocationId == fromId)
                .Sum(m => m.Quantity);

            int available = incoming - outgoing;

            if (available < request.Quantity)
            {
                return Results.BadRequest(new
                {
                    Message = "Insufficient stock.",
                    Available = available,
                    Requested = request.Quantity
                });
            }
        }

        // 5. Validate the destination location.
        if (request.ToLocationId is int toId)
        {
            bool destinationExists = await db.StockLocations
                .AnyAsync(l => l.Id == toId);

            if (!destinationExists)
            {
                return Results.BadRequest(new
                {
                    Message = "Destination location does not exist."
                });
            }
        }

        // 6. Create a new movement record.
        var movement = new StockMovement
        {
            ProductId = request.ProductId,
            FromLocationId = request.FromLocationId,
            ToLocationId = request.ToLocationId,
            Quantity = request.Quantity,
            Reference = reference,
            OccurredAtUtc = DateTime.UtcNow
        };

        db.StockMovements.Add(movement);

        // 7. Save while the transaction is active.
        await db.SaveChangesAsync();

        // 8. Commit the complete operation.
        await transaction.CommitAsync();

        return Results.Created(
            $"/stock-movements/{movement.Id}",
            movement);
    }
    catch (SqliteException ex)
        when (ex.SqliteErrorCode is 5 or 6)
    {
        // SQLite was busy or locked beyond its timeout.
        return Results.Json(new
        {
            Message = "Inventory database is temporarily busy. Check movement history before retrying."
        }, statusCode: 503);
    }
    catch (DbUpdateException ex)
        when (ex.InnerException is SqliteException sqlite &&
              sqlite.SqliteErrorCode is 5 or 6)
    {
        return Results.Json(new
        {
            Message = "Inventory database is temporarily busy. Check movement history before retrying."
        }, statusCode: 503);
    }
});

// Endpoint 9: Get a specific stock movement by ID
app.MapGet("/stock-movements/{id:int}",
    async (int id, InventoryDbContext db) =>
{
    var movement = await db.StockMovements
        .AsNoTracking()
        .FirstOrDefaultAsync(m => m.Id == id);

    if (movement is null)
    {
        return Results.NotFound(new
        {
            Message = $"Movement {id} was not found."
        });
    }

    return Results.Ok(movement);
});

// Endpoint 10: Get all stock movements, optionally filtered by product ID
app.MapGet("/stock-movements",
    async (int? productId, InventoryDbContext db) =>
{
    var query = db.StockMovements
        .AsNoTracking()
        .AsQueryable();

    if (productId.HasValue)
    {
        query = query.Where(m =>
            m.ProductId == productId.Value);
    }

    var movements = await query
        .OrderByDescending(m => m.Id)
        .ToListAsync();

    return Results.Ok(movements);
});

// Endpoint 11: Reverse a stock movement
app.MapPost("/stock-movements/{id:int}/reverse",
    async (int id,
           ReverseStockMovementRequest request,
           InventoryDbContext db) =>
{
    // 1. Validate the correction reason.
    string reason = request.Reason?.Trim() ?? "";

    if (reason.Length < 1 || reason.Length > 200)
    {
        return Results.BadRequest(new
        {
            Message = "Reason must contain 1 to 200 characters."
        });
    }

    try
    {
        // 2. Protect the entire reversal operation.
        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);


        // 3. Find the original movement.
        var original = await db.StockMovements
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (original is null)
        {
            return Results.NotFound(new
            {
                Message = $"Movement {id} was not found."
            });
        }

        // NEW: Prevent reversing sales-linked movements.
        bool belongsToSale = await db.SaleItems
            .AnyAsync(item => item.StockMovementId == id);

        if (belongsToSale)
        {
            return Results.Conflict(new
            {
                Message =
                    "This movement belongs to a sale. " +
                    "Use the sales correction workflow."
            });
        }

        // 4. Do not allow reversing a reversal.
        if (original.ReversesMovementId is not null)
        {
            return Results.Conflict(new
            {
                Message = "A reversal cannot be reversed."
            });
        }

        // Continue with the existing reversal logic...

        // 5. Check whether this movement was already reversed.
        bool alreadyReversed = await db.StockMovements
            .AnyAsync(m => m.ReversesMovementId == id);

        if (alreadyReversed)
        {
            return Results.Conflict(new
            {
                Message = "This movement has already been reversed."
            });
        }

        // 6. Reverse the movement direction.
        int? reversalSource = original.ToLocationId;
        int? reversalDestination = original.FromLocationId;

        // 7. Check available stock if the reversal removes stock.
        if (reversalSource is int fromId)
        {
            var movements = await db.StockMovements
                .AsNoTracking()
                .Where(m =>
                    m.ProductId == original.ProductId &&
                    (m.FromLocationId == fromId ||
                     m.ToLocationId == fromId))
                .ToListAsync();

            int incoming = movements
                .Where(m => m.ToLocationId == fromId)
                .Sum(m => m.Quantity);

            int outgoing = movements
                .Where(m => m.FromLocationId == fromId)
                .Sum(m => m.Quantity);

            int available = incoming - outgoing;

            if (available < original.Quantity)
            {
                return Results.BadRequest(new
                {
                    Message = "Cannot reverse: insufficient stock at the reversal source.",
                    Available = available,
                    Required = original.Quantity
                });
            }
        }

        // 8. Create a new compensating movement.
        var reversal = new StockMovement
        {
            ProductId = original.ProductId,
            FromLocationId = reversalSource,
            ToLocationId = reversalDestination,
            Quantity = original.Quantity,
            OccurredAtUtc = DateTime.UtcNow,
            Reference = $"REV-{original.Id}",
            ReversesMovementId = original.Id,
            ReversalReason = reason
        };

        db.StockMovements.Add(reversal);

        // 9. Save and commit.
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Created(
            $"/stock-movements/{reversal.Id}",
            reversal);
    }
    catch (DbUpdateException ex)
        when (ex.InnerException is SqliteException sqlite &&
              sqlite.SqliteErrorCode == 19 &&
              sqlite.SqliteExtendedErrorCode == 2067)
    {
        // Database rejected a duplicate reversal.
        return Results.Conflict(new
        {
            Message = "This movement has already been reversed."
        });
    }
    catch (SqliteException ex)
        when (ex.SqliteErrorCode is 5 or 6)
    {
        return Results.Json(new
        {
            Message = "Inventory database is temporarily busy."
        }, statusCode: 503);
    }
    catch (DbUpdateException ex)
        when (ex.InnerException is SqliteException sqlite &&
              sqlite.SqliteErrorCode is 5 or 6)
    {
        return Results.Json(new
        {
            Message = "Inventory database is temporarily busy."
        }, statusCode: 503);
    }


});

app.MapPost("/sales",
    async (CreateSaleRequest request, InventoryDbContext db) =>
{
    // 1. Validate the request.
    if (request.Items is null || request.Items.Count == 0)
    {
        return Results.BadRequest(new
        {
            Message = "A sale must contain at least one item."
        });
    }

    // Validate excluded amount.
    decimal excludedAmount = request.ExcludedAmount ?? 0m;

    if (excludedAmount < 0m ||
        excludedAmount != decimal.Round(excludedAmount, 2))
    {
        return Results.BadRequest(new
        {
            Message =
                "Excluded amount must be non-negative and use at most two decimal places."
        });
    }

    // Clean and validate lady names.
    var ladyNames = request.LadyNames?
        .Select(name => name?.Trim() ?? string.Empty)
        .ToList() ?? new List<string>();

    if (ladyNames.Count > 8)
    {
        return Results.BadRequest(new
        {
            Message = "A receipt cannot have more than eight ladies."
        });
    }

    if (ladyNames.Any(name =>
        name.Length is < 1 or > 120))
    {
        return Results.BadRequest(new
        {
            Message = "Each lady name must contain 1 to 120 characters."
        });
    }

    if (ladyNames.Distinct(
        StringComparer.OrdinalIgnoreCase).Count()
        != ladyNames.Count)
    {
        return Results.BadRequest(new
        {
            Message = "Duplicate lady names are not allowed."
        });
    }

    if (request.Items.Count > 100)
    {
        return Results.BadRequest(new
        {
            Message = "A sale cannot contain more than 100 lines."
        });
    }

    if (request.Items.Any(item =>
        item is null ||
        item.ProductId <= 0 ||
        item.FromLocationId <= 0 ||
        item.Quantity <= 0))
    {
        return Results.BadRequest(new
        {
            Message = "Every item needs a valid product, location and positive quantity."
        });
    }

    try
    {
        // 2. Begin transaction before reading products or stock.
        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        // 3. Fetch the products and locations.
        int[] productIds = request.Items
            .Select(i => i.ProductId)
            .Distinct()
            .ToArray();

        int[] locationIds = request.Items
            .Select(i => i.FromLocationId)
            .Distinct()
            .ToArray();

        var products = await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var locations = await db.StockLocations
            .AsNoTracking()
            .Where(l => locationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id);

        // 4. Validate all products and sale locations.
        foreach (var item in request.Items)
        {
            if (!products.ContainsKey(item.ProductId))
            {
                return Results.BadRequest(new
                {
                    Message = $"Product {item.ProductId} does not exist."
                });
            }

            if (!locations.TryGetValue(
                item.FromLocationId, out var location))
            {
                return Results.BadRequest(new
                {
                    Message = $"Location {item.FromLocationId} does not exist."
                });
            }

            if (!location.IsUsable)
            {
                return Results.BadRequest(new
                {
                    Message = $"Sales cannot issue stock from {location.Name}."
                });
            }
        }

        // 5. Calculate stock requirements.
        // Grouping prevents duplicate lines from overselling.
        var requirements = request.Items
            .GroupBy(item => new
            {
                item.ProductId,
                item.FromLocationId
            })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.FromLocationId,
                Required = group.Sum(i => (long)i.Quantity)
            })
            .ToList();

        // 6. Load relevant inventory history.
        var movements = await db.StockMovements
            .AsNoTracking()
            .Where(m => productIds.Contains(m.ProductId))
            .ToListAsync();

        // 7. Check every product-location balance.
        foreach (var requirement in requirements)
        {
            long incoming = movements
                .Where(m =>
                    m.ProductId == requirement.ProductId &&
                    m.ToLocationId == requirement.FromLocationId)
                .Sum(m => (long)m.Quantity);

            long outgoing = movements
                .Where(m =>
                    m.ProductId == requirement.ProductId &&
                    m.FromLocationId == requirement.FromLocationId)
                .Sum(m => (long)m.Quantity);

            long available = incoming - outgoing;

            if (available < requirement.Required)
            {
                return Results.BadRequest(new
                {
                    Message = "Insufficient stock for sale.",
                    requirement.ProductId,
                    requirement.FromLocationId,
                    Available = available,
                    Required = requirement.Required
                });
            }
        }

        // 8. Capture the timestamp ONCE for the whole sale.
        DateTime soldAtUtc = DateTime.UtcNow;

        // Get the business date using the 3 AM Singapore cutoff.
        var businessDate =
            BusinessDateCalculator.GetBusinessDate(soldAtUtc);

        // Find the last receipt sequence for this business date.
        // Existing sales without receipt numbers are ignored.
        int lastSequence = await db.Sales
            .Where(s =>
                s.BusinessDate == businessDate &&
                s.ReceiptSequence != null)
            .OrderByDescending(s => s.ReceiptSequence)
            .Select(s => s.ReceiptSequence)
            .FirstOrDefaultAsync() ?? 0;

        // Generate the next receipt sequence.
        int nextSequence = lastSequence + 1;

        // Format: YYYYMMDD-001, YYYYMMDD-002, etc.
        string receiptNumber =
            $"{businessDate:yyyyMMdd}-{nextSequence:D3}";

        // Create the sale with its receipt information.
        var sale = new Sale
        {
            SoldAtUtc = soldAtUtc,
            BusinessDate = businessDate,
            ReceiptSequence = nextSequence,
            ReceiptNumber = receiptNumber,
            ExcludedAmount = excludedAmount
        };

        // 9. Save sale to generate its ID.
        db.Sales.Add(sale);
        await db.SaveChangesAsync();

        // 10. Create an inventory deduction for each sale line.
        var stockMovements = request.Items
            .Select((item, index) => new StockMovement
            {
                ProductId = item.ProductId,
                FromLocationId = item.FromLocationId,
                ToLocationId = null,
                Quantity = item.Quantity,
                OccurredAtUtc = soldAtUtc,
                Reference = $"SALE-{sale.Id}-ITEM-{index + 1}"
            })
            .ToList();

        db.StockMovements.AddRange(stockMovements);
        await db.SaveChangesAsync();

        // 11. Save historical product prices, costs and BACK.
        var saleItems = new List<SaleItem>();

        for (int i = 0; i < request.Items.Count; i++)
        {
            var requestedItem = request.Items[i];
            var product = products[requestedItem.ProductId];

            saleItems.Add(new SaleItem
            {
                SaleId = sale.Id,
                ProductId = product.Id,
                FromLocationId = requestedItem.FromLocationId,
                Quantity = requestedItem.Quantity,

                // Historical product information
                ProductNameSnapshot = product.Name,
                CategorySnapshot = product.Category,

                // Historical prices
                UnitSellingPrice = product.SellingPrice,
                UnitPurchaseCost = product.PurchaseCost,

                // Calculate BACK at the time of sale
                BackAmount = BackCalculator.Calculate(
                    product.Category,
                    product.SellingPrice,
                    product.PurchaseCost,
                    requestedItem.Quantity),

                // Linked inventory deduction
                StockMovementId = stockMovements[i].Id
            });

        }

        db.SaleItems.AddRange(saleItems);
        await db.SaveChangesAsync();

        // 12. Calculate financial totals in C#.
        decimal totalRevenue = saleItems.Sum(item =>
            item.UnitSellingPrice * item.Quantity);

        decimal totalCost = saleItems.Sum(item =>
            item.UnitPurchaseCost * item.Quantity);

        decimal grossProfit = totalRevenue - totalCost;

        // Calculate total BACK for this receipt.
        decimal? totalBack = saleItems.Any(
            item => item.BackAmount is null)
            ? null
            : saleItems.Sum(item => item.BackAmount!.Value);

        // The excluded amount cannot exceed receipt revenue.
        if (excludedAmount > totalRevenue)
        {
            return Results.BadRequest(new
            {
                Message = "Excluded amount cannot exceed receipt revenue."
            });
        }

        decimal eligibleSales = totalRevenue - excludedAmount;

        // Keep the original sale totals separate from allocations.
        var allocations = new List<SaleAllocation>();

        if (ladyNames.Count > 0)
        {
            // We allocate sales and costs in cents,
            // and BACK to four decimal places.
            if (totalRevenue != decimal.Round(totalRevenue, 2) ||
                totalCost != decimal.Round(totalCost, 2) ||
                totalBack is null ||
                totalBack.Value != decimal.Round(totalBack.Value, 4))
            {
                return Results.BadRequest(new
                {
                    Message = "Sale amounts have unsupported decimal precision."
                });
            }

            var salesShares = AllocationSplitter.Split(
                eligibleSales, ladyNames.Count, 2);

            var costShares = AllocationSplitter.Split(
                totalCost, ladyNames.Count, 2);

            var backShares = AllocationSplitter.Split(
                totalBack.Value, ladyNames.Count, 4);

            for (int i = 0; i < ladyNames.Count; i++)
            {
                allocations.Add(new SaleAllocation
                {
                    SaleId = sale.Id,
                    Position = i + 1,
                    LadyNameSnapshot = ladyNames[i],
                    AllocatedEligibleSales = salesShares[i],
                    AllocatedBottleCost = costShares[i],
                    AllocatedBack = backShares[i]
                });
            }

            db.SaleAllocations.AddRange(allocations);
            await db.SaveChangesAsync();
        }

        // 13. Commit ALL database changes together.
        await transaction.CommitAsync();



        // 14. Return the newly created sale.
        return Results.Created($"/sales/{sale.Id}", new
        {
            sale.Id,
            sale.SoldAtUtc,
            sale.BusinessDate,
            sale.ReceiptSequence,
            sale.ReceiptNumber,
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            GrossProfit = grossProfit,
            TotalBack = totalBack,

            // Sales allocation information
            ExcludedAmount = excludedAmount,
            EligibleSales = eligibleSales,

            Allocations = allocations
    .OrderBy(a => a.Position)
    .Select(a => new
    {
        a.Position,
        a.LadyNameSnapshot,
        a.AllocatedEligibleSales,
        a.AllocatedBottleCost,
        NetSales = a.AllocatedEligibleSales
            - a.AllocatedBottleCost,
        a.AllocatedBack
    }).ToList(),

            Items = saleItems.Select(item => new
            {
                item.Id,
                item.ProductId,
                item.ProductNameSnapshot,
                item.CategorySnapshot,
                item.FromLocationId,
                item.Quantity,
                item.UnitSellingPrice,
                item.UnitPurchaseCost,
                item.BackAmount,
                item.StockMovementId,
                LineRevenue =
                                            item.UnitSellingPrice * item.Quantity,
                LineCost =
                                            item.UnitPurchaseCost * item.Quantity
            }).ToList()


        });


    }
    catch (SqliteException ex)
        when (ex.SqliteErrorCode is 5 or 6)
    {
        return Results.Json(new
        {
            Message = "Inventory database is temporarily busy."
        }, statusCode: 503);
    }
    catch (DbUpdateException ex)
        when (ex.InnerException is SqliteException sqlite &&
              sqlite.SqliteErrorCode is 5 or 6)
    {
        return Results.Json(new
        {
            Message = "Inventory database is temporarily busy."
        }, statusCode: 503);
    }
});

app.MapGet("/sales/{id:int}",
    async (int id, InventoryDbContext db) =>
{
    // Find the sale and include its items.
    var sale = await db.Sales
        .AsNoTracking()
        .Include(s => s.Items)
        .Include(s => s.Allocations)
        .AsSplitQuery()
        .FirstOrDefaultAsync(s => s.Id == id);
    if (sale is null)
    {
        return Results.NotFound(new
        {
            Message = $"Sale {id} was not found."
        });
    }

    // Calculate totals using saved historical prices.
    decimal revenue = sale.Items.Sum(i =>
        i.UnitSellingPrice * i.Quantity);

    decimal cost = sale.Items.Sum(i =>
        i.UnitPurchaseCost * i.Quantity);

    // Sum the historical BACK amounts.
    // Null indicates an older sale that hasn't been evaluated.
    decimal? totalBack = sale.Items.Any(
        item => item.BackAmount is null)
        ? null
        : sale.Items.Sum(item => item.BackAmount!.Value);

    return Results.Ok(new
    {
        sale.Id,
        sale.SoldAtUtc,
        sale.BusinessDate,
        sale.ReceiptSequence,
        sale.ReceiptNumber,
        TotalRevenue = revenue,
        TotalCost = cost,
        GrossProfit = revenue - cost,
        TotalBack = totalBack,

        // Historical sales allocation information
        ExcludedAmount = sale.ExcludedAmount,

        EligibleSales = sale.ExcludedAmount is null
    ? (decimal?)null
    : revenue - sale.ExcludedAmount.Value,

        Allocations = sale.Allocations
    .OrderBy(a => a.Position)
    .Select(a => new
    {
        a.Position,
        a.LadyNameSnapshot,
        a.AllocatedEligibleSales,
        a.AllocatedBottleCost,
        NetSales = a.AllocatedEligibleSales
            - a.AllocatedBottleCost,
        a.AllocatedBack
    }).ToList(),

        Items = sale.Items.OrderBy(i => i.Id).ToList()
    });
});

// Day 4: Daily ladies' sales report.
app.MapGet("/reports/ladies/daily",
    async (string? date, InventoryDbContext db) =>
{
    // 1. Require a valid YYYY-MM-DD date.
    if (string.IsNullOrWhiteSpace(date) ||
        !DateOnly.TryParseExact(
            date,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var businessDate))
    {
        return Results.BadRequest(new
        {
            Message = "Date must use YYYY-MM-DD format."
        });
    }

    // 2. Load historical allocations for this business date.
    var allocations = await db.SaleAllocations
        .AsNoTracking()
        .Where(a => a.Sale.BusinessDate == businessDate)
        .ToListAsync();

    // 3. Group allocations and calculate each lady's totals.
    var ladies = LadyDailySummaryCalculator.Calculate(
        allocations);

    // 4. Calculate report totals.
    decimal totalSales = ladies.Sum(l => l.Sales);

    decimal totalBottleCost = ladies.Sum(l =>
        l.TotalBottleCost);

    decimal totalNetSales = ladies.Sum(l =>
        l.NetSales);

    decimal totalBack = ladies.Sum(l => l.Back);

    // 5. Return the daily report.
    return Results.Ok(new
    {
        BusinessDate = businessDate,
        LadyCount = ladies.Count,

        Totals = new
        {
            Sales = totalSales,
            TotalBottleCost = totalBottleCost,
            NetSales = totalNetSales,
            Back = totalBack
        },

        Ladies = ladies
    });
});

// Day 4: Monthly ladies' sales report.
app.MapGet("/reports/ladies/monthly",
    async (int? year, int? month, InventoryDbContext db) =>
{
    // 1. Validate the requested year and month.
    if (year is null || month is null ||
        year < 1 || year > 9999 ||
        month < 1 || month > 12)
    {
        return Results.BadRequest(new
        {
            Message =
                "A valid year (1-9999) and month (1-12) are required."
        });
    }

    // 2. Calculate the first and last business dates.
    var startDate = new DateOnly(
        year.Value, month.Value, 1);

    var endDate = new DateOnly(
        year.Value,
        month.Value,
        DateTime.DaysInMonth(year.Value, month.Value));

    // 3. Load allocations within the selected month.
    var allocations = await db.SaleAllocations
        .AsNoTracking()
        .Where(a =>
            a.Sale.BusinessDate >= startDate &&
            a.Sale.BusinessDate <= endDate)
        .ToListAsync();

    // 4. Reuse our existing summary calculator.
    var ladies =
        LadyDailySummaryCalculator.Calculate(allocations);

    // 5. Calculate the combined report totals.
    decimal totalSales = ladies.Sum(l => l.Sales);

    decimal totalBottleCost = ladies.Sum(l =>
        l.TotalBottleCost);

    decimal totalNetSales = ladies.Sum(l =>
        l.NetSales);

    decimal totalBack = ladies.Sum(l => l.Back);

    // 6. Return the monthly report.
    return Results.Ok(new
    {
        Year = year.Value,
        Month = month.Value,
        PeriodStart = startDate,
        PeriodEnd = endDate,
        LadyCount = ladies.Count,

        Totals = new
        {
            Sales = totalSales,
            TotalBottleCost = totalBottleCost,
            NetSales = totalNetSales,
            Back = totalBack
        },

        Ladies = ladies
    });
});

// Day 4: Previous completed business month's ladies report.
app.MapGet("/reports/ladies/previous-month",
    async (InventoryDbContext db) =>
{
    // 1. Determine the most recently completed
    // business month using the Singapore 3 AM cutoff.
    var (startDate, endDate) =
        PreviousCompletedMonthCalculator.GetPeriod(
            DateTime.UtcNow);

    // 2. Retrieve historical allocations.
    var allocations = await db.SaleAllocations
        .AsNoTracking()
        .Where(a =>
            a.Sale.BusinessDate >= startDate &&
            a.Sale.BusinessDate <= endDate)
        .ToListAsync();

    // 3. Reuse the existing reporting calculator.
    var ladies =
        LadyDailySummaryCalculator.Calculate(allocations);

    // 4. Return the completed month's report.
    return Results.Ok(new
    {
        Year = startDate.Year,
        Month = startDate.Month,
        PeriodStart = startDate,
        PeriodEnd = endDate,
        LadyCount = ladies.Count,

        Totals = new
        {
            Sales = ladies.Sum(l => l.Sales),
            TotalBottleCost = ladies.Sum(l =>
                l.TotalBottleCost),
            NetSales = ladies.Sum(l => l.NetSales),
            Back = ladies.Sum(l => l.Back)
        },

        Ladies = ladies
    });
});

app.Run();

public partial class Program { }