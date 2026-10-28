using InventoryApi.Data;
using InventoryApi.Models;
using Microsoft.EntityFrameworkCore;


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
    // Find the product
    var product = await db.Products.FindAsync(id);

    // Product does not exist
    if (product is null)
    {
        return Results.NotFound(new
        {
            Message = $"Product with ID {id} was not found."
        });
    }

    // Mark the product for deletion
    db.Products.Remove(product);

    // Delete from SQLite
    await db.SaveChangesAsync();

    // Return HTTP 204
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
    // 1. Validate the product and quantity
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

    bool productExists = await db.Products
        .AnyAsync(p => p.Id == request.ProductId);

    if (!productExists)
    {
        return Results.BadRequest(new
        {
            Message = "Product does not exist."
        });
    }

    // 2. Validate the movement direction
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

    // 3. Validate optional reference
    string? reference = request.Reference?.Trim();

    if (reference?.Length > 100)
    {
        return Results.BadRequest(new
        {
            Message = "Reference cannot exceed 100 characters."
        });
    }

    // 4. Validate source location
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

        // Retrieve stock history for the source
        var movements = await db.StockMovements
            .AsNoTracking()
            .Where(m =>
                m.ProductId == request.ProductId &&
                (m.FromLocationId == fromId ||
                 m.ToLocationId == fromId))
            .ToListAsync();

        // Calculate stock available at the source
        int available = movements.Sum(m =>
            (m.ToLocationId == fromId ? m.Quantity : 0)
            -
            (m.FromLocationId == fromId ? m.Quantity : 0));

        // Prevent negative stock
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

    // 5. Validate destination location
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

    // 6. Create the stock movement
    var movement = new StockMovement
    {
        ProductId = request.ProductId,
        FromLocationId = request.FromLocationId,
        ToLocationId = request.ToLocationId,
        Quantity = request.Quantity,
        Reference = reference,
        OccurredAtUtc = DateTime.UtcNow
    };

    // 7. Save to SQLite
    db.StockMovements.Add(movement);
    await db.SaveChangesAsync();

    // 8. Return HTTP 201 Created
    return Results.Created(
        $"/stock-movements/{movement.Id}",
        movement);
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

app.Run();