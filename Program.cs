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

app.Run();