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
    public DbSet<SaleAllocation> SaleAllocations
        => Set<SaleAllocation>();
    // Sales tables
    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

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

        // Configure sales
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.ToTable("Sales");

            // Primary key
            entity.HasKey(s => s.Id);

            // Date and time when the sale was recorded
            entity.Property(s => s.SoldAtUtc)
                .IsRequired();

            // Singapore business date
            entity.Property(s => s.BusinessDate)
                .IsRequired();

            // Index to support historical business-date queries
            entity.HasIndex(s => s.BusinessDate);
        });
        // Configure sale items
        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.ToTable("SaleItems");

            // Primary key
            entity.HasKey(item => item.Id);

            // Validate maximum snapshot lengths
            entity.Property(item => item.ProductNameSnapshot)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(item => item.CategorySnapshot)
                .IsRequired()
                .HasMaxLength(60);

            // One Sale can have many SaleItems
            entity.HasOne<Sale>()
                .WithMany(s => s.Items)
                .HasForeignKey(item => item.SaleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Every SaleItem refers to an existing Product
            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Every SaleItem has an inventory source location
            entity.HasOne<StockLocation>()
                .WithMany()
                .HasForeignKey(item => item.FromLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Optional one-to-one link to its stock deduction
            entity.HasOne<StockMovement>()
                .WithOne()
                .HasForeignKey<SaleItem>(
                    item => item.StockMovementId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(i => i.CategorySnapshot)
                .HasMaxLength(60);

            entity.Property(i => i.BackAmount)
                .HasPrecision(18, 4);
        });

        // Receipt numbers must be unique.
        modelBuilder.Entity<Sale>()
            .HasIndex(s => s.ReceiptNumber)
            .IsUnique();

        // Each business date can use a sequence number only once.
        modelBuilder.Entity<Sale>()
            .HasIndex(s => new
            {
                s.BusinessDate,
                s.ReceiptSequence
            })
            .IsUnique();

        // Configure sales allocations.
        modelBuilder.Entity<SaleAllocation>(entity =>
        {
            entity.Property(a => a.LadyNameSnapshot)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(a => a.AllocatedEligibleSales)
                .HasPrecision(18, 2);

            entity.Property(a => a.AllocatedBottleCost)
                .HasPrecision(18, 2);

            entity.Property(a => a.AllocatedBack)
                .HasPrecision(18, 4);

            // Only one lady can occupy a receipt position.
            entity.HasIndex(a => new
            {
                a.SaleId,
                a.Position
            }).IsUnique();
        });

        // Link each allocation to its parent sale.
        modelBuilder.Entity<Sale>()
            .HasMany(s => s.Allocations)
            .WithOne(a => a.Sale)
            .HasForeignKey(a => a.SaleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Store the receipt's excluded amount.
        modelBuilder.Entity<Sale>()
            .Property(s => s.ExcludedAmount)
            .HasPrecision(18, 2);
    }


}