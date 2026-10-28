using InventoryApi.Models;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var products = new List<Product>
{
    new Product
    {
        Id = 1,
        Name = "Demo Champagne",
        Category = "Champagne",
        PurchaseCost = 75.00m,
        SellingPrice = 180.00m
    },

    new Product
    {
        Id = 2,
        Name = "Demo Whisky",
        Category = "Whisky",
        PurchaseCost = 50.00m,
        SellingPrice = 120.00m
    },

    new Product
    {
        Id = 3,
        Name = "Demo Sake",
        Category = "Sake",
        PurchaseCost = 35.00m,
        SellingPrice = 90.00m
    }
};

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

// Endpoint 4: Get a specific product by ID
app.MapGet("/products/{id:int}", (int id) =>
{
    Product? product = products.FirstOrDefault(p => p.Id == id);

    if (product is null)
    {
        return Results.NotFound(new
        {
            Message = $"Product with ID {id} was not found."
        });
    }

    return Results.Ok(product);
});

app.MapPost("/products", (CreateProductRequest request) =>
{
    // Clean incoming text
    string name = request.Name?.Trim() ?? "";
    string category = request.Category?.Trim() ?? "";

    // Validate the product name
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest(new
        {
            Message = "Product name is required."
        });
    }

    // Validate the category
    if (string.IsNullOrWhiteSpace(category))
    {
        return Results.BadRequest(new
        {
            Message = "Product category is required."
        });
    }

    // Validate the purchase cost
    if (request.PurchaseCost is null ||
        request.PurchaseCost < 0)
    {
        return Results.BadRequest(new
        {
            Message = "Purchase cost must be zero or greater."
        });
    }

    // Validate the selling price
    if (request.SellingPrice is null ||
        request.SellingPrice <= 0)
    {
        return Results.BadRequest(new
        {
            Message = "Selling price must be greater than zero."
        });
    }

    // Generate a unique ID for the current list
    int nextId = products.Count == 0
        ? 1
        : products.Max(p => p.Id) + 1;

    // Create the new product
    var product = new Product
    {
        Id = nextId,
        Name = name,
        Category = category,
        PurchaseCost = request.PurchaseCost.Value,
        SellingPrice = request.SellingPrice.Value
    };

    // Store the product in memory
    products.Add(product);

    // Return HTTP 201 Created
    return Results.Created($"/products/{product.Id}", product);
});

app.Run();