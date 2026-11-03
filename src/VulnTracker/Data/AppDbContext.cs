using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VulnTracker.Security;

namespace VulnTracker.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, FieldEncryptor encryptor) : DbContext(options)
{
    public DbSet<Finding> Findings => Set<Finding>();

    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Store enums as text so the database is readable.
        var finding = modelBuilder.Entity<Finding>();
        finding.Property(f => f.Severity).HasConversion<string>();
        finding.Property(f => f.Status).HasConversion<string>();
        finding.Property(f => f.Source).HasConversion<string>();

        // FIND-11: encrypted in the database, plaintext in the app.
        // These columns can't be searched or sorted in SQL. Filters use the unencrypted columns.
        finding.Property(f => f.Title).HasConversion(Encrypted("Finding.Title"));
        finding.Property(f => f.Description).HasConversion(Encrypted("Finding.Description"));
        finding.Property(f => f.ReporterEmail).HasConversion(Encrypted("Finding.ReporterEmail"));
    }

    // Returned as the base type so it fits both string and string? properties. EF never passes null to it.
    private ValueConverter Encrypted(string purpose)
    {
        var fields = encryptor;
        return new ValueConverter<string, string>(
            plaintext => fields.Encrypt(plaintext, purpose),
            stored => fields.Decrypt(stored, purpose));
    }
}
