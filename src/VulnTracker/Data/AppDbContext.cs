using Microsoft.EntityFrameworkCore;

namespace VulnTracker.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Finding> Findings => Set<Finding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Store enums as text so the database is readable.
        var finding = modelBuilder.Entity<Finding>();
        finding.Property(f => f.Severity).HasConversion<string>();
        finding.Property(f => f.Status).HasConversion<string>();
        finding.Property(f => f.Source).HasConversion<string>();
    }
}
