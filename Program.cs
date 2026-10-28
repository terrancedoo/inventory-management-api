var builder = WebApplication.CreateBuilder(args);

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
// Endpoint 3: Exercise
app.MapGet("/Exercise", () =>
{
    return Results.Ok(new
    {
        developer = "Terrance",
        language = "C#",
        project = "Inventory Management System",
    });
});
app.Run();