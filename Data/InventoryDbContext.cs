using InventoryApi.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(
        DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
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
    }
}