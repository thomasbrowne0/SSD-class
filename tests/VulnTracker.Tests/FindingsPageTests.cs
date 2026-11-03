using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VulnTracker.Data;

namespace VulnTracker.Tests;

// Uses a separate database so tests don't touch dev data. Needs Postgres running (docker compose up db).
public class TestAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] =
                "Host=localhost;Port=5432;Database=vulntracker_test;Username=postgres;Password=postgres",
        }));
    }
}

public class FindingsPageTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Fact]
    public async Task Index_lists_saved_findings()
    {
        var title = $"SQL injection in login {Guid.NewGuid()}";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Findings.Add(new Finding
            {
                Title = title,
                Description = "Login form concatenates input into SQL.",
                AffectedAsset = "auth-service",
                Severity = Severity.High,
                DiscoveryDate = new DateOnly(2026, 11, 3),
                Source = FindingSource.Internal,
            });
            await db.SaveChangesAsync();
        }

        var html = await factory.CreateClient().GetStringAsync("/");

        Assert.Contains(title, html);
    }
}
