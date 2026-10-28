using InventoryApi.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Data;

public class InventoryDbContext : DbContext
{
    // Constructor
    public InventoryDbContext(
        DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    // Database tables
    public DbSet<Product> Products => Set<Product>();

    public DbSet<StockLocation> StockLocations
        => Set<StockLocation>();

    public DbSet<StockMovement> StockMovements
        => Set<StockMovement>();

    // Database configuration
    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        // Product configuration
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(p => p.Category)
                .IsRequired()
                .HasMaxLength(60);
        });

        // Stock location configuration
        modelBuilder.Entity<StockLocation>(entity =>
        {
            entity.HasKey(l => l.Id);

            entity.Property(l => l.Name)
                .IsRequired()
                .HasMaxLength(60);

            // Initial inventory locations
            entity.HasData(
                new StockLocation
                {
                    Id = 1,
                    Name = "Store",
                    IsUsable = true
                },
                new StockLocation
                {
                    Id = 2,
                    Name = "Locker",
                    IsUsable = false
                },
                new StockLocation
                {
                    Id = 3,
                    Name = "Office",
                    IsUsable = false
                },
                new StockLocation
                {
                    Id = 4,
                    Name = "Wine Chiller",
                    IsUsable = true
                }
            );
        });

        // Stock movement configuration
        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(m => m.Id);

            entity.Property(m => m.Reference)
                .HasMaxLength(100);

            // Relationship with Product
            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with source location
            entity.HasOne<StockLocation>()
                .WithMany()
                .HasForeignKey(m => m.FromLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with destination location
            entity.HasOne<StockLocation>()
                .WithMany()
                .HasForeignKey(m => m.ToLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index for product movement queries
            entity.HasIndex(m => new
            {
                m.ProductId,
                m.OccurredAtUtc
            });
            // Limit reversal reasons to 200 characters.
            entity.Property(m => m.ReversalReason)
                .HasMaxLength(200);

            // Each reversal references its original movement.
            entity.HasOne<StockMovement>()
                .WithMany()
                .HasForeignKey(m => m.ReversesMovementId)
                .OnDelete(DeleteBehavior.Restrict);

            // A movement can be reversed at most once.
            entity.HasIndex(m => m.ReversesMovementId)
                .IsUnique();

        });
    }
}