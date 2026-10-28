using InventoryApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InventoryApi.Tests;

public class InventoryApiFactory : WebApplicationFactory<Program>
{
    // Each factory gets a different temporary database.
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"inventory-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        // The main API project is at the repository root.
        string projectRoot = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", ".."));

        builder.UseContentRoot(projectRoot);

        builder.ConfigureTestServices(services =>
        {
            // Remove the normal database configuration.
            services.RemoveAll<
                IDbContextOptionsConfiguration<InventoryDbContext>>();

            services.RemoveAll<
                DbContextOptions<InventoryDbContext>>();

            services.RemoveAll<InventoryDbContext>();

            // Replace it with an isolated SQLite database.
            services.AddDbContext<InventoryDbContext>(options =>
                options.UseSqlite(
                    $"Data Source={_databasePath};Default Timeout=10"));
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        // Apply the real migrations, including location seed data.
        await db.Database.MigrateAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            SqliteConnection.ClearAllPools();

            foreach (string path in new[]
            {
                _databasePath,
                _databasePath + "-wal",
                _databasePath + "-shm"
            })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}