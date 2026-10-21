using Microsoft.EntityFrameworkCore;
using StockTransfer.Api.Domain;

namespace StockTransfer.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Stock> Stock => Set<Stock>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<TransferLine> TransferLines => Set<TransferLine>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Branch>(e =>
        {
            e.Property(b => b.Name).HasMaxLength(100);
            e.HasIndex(b => b.Name).IsUnique();
            e.Property(b => b.Type).HasConversion<string>().HasMaxLength(20);
            e.HasData(SeedData.Branches);
        });

        model.Entity<Product>(e =>
        {
            e.Property(p => p.Sku).HasMaxLength(50);
            e.HasIndex(p => p.Sku).IsUnique();
            e.Property(p => p.Name).HasMaxLength(200);
            e.HasData(SeedData.Products);
        });

        model.Entity<Stock>(e =>
        {
            e.HasKey(s => new { s.BranchId, s.ProductId });
            // Last line of defence: even a bug in the application code cannot drive stock negative.
            e.ToTable(t => t.HasCheckConstraint("CK_Stock_Quantity_NonNegative", "\"Quantity\" >= 0"));
            e.HasOne(s => s.Branch).WithMany().HasForeignKey(s => s.BranchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Product).WithMany().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasData(SeedData.Stock());
        });

        model.Entity<Transfer>(e =>
        {
            e.ToTable(t => t.HasCheckConstraint("CK_Transfer_DifferentBranches", "\"FromBranchId\" <> \"ToBranchId\""));
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.IdempotencyKey).HasMaxLength(100);
            e.HasIndex(t => t.IdempotencyKey).IsUnique();
            e.HasIndex(t => t.Status);
            e.HasOne(t => t.FromBranch).WithMany().HasForeignKey(t => t.FromBranchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.ToBranch).WithMany().HasForeignKey(t => t.ToBranchId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(t => t.Lines).WithOne().HasForeignKey(l => l.TransferId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<TransferLine>(e =>
        {
            e.ToTable(t => t.HasCheckConstraint("CK_TransferLine_Quantity_Positive", "\"Quantity\" > 0"));
            // A product appears at most once per transfer.
            e.HasIndex(l => new { l.TransferId, l.ProductId }).IsUnique();
            e.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
