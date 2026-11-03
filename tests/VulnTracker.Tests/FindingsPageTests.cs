using System.Net;
using Microsoft.Extensions.DependencyInjection;
using VulnTracker.Data;

namespace VulnTracker.Tests;

public class FindingsPageTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Fact]
    public async Task Index_requires_login()
    {
        var response = await factory.CreateAnonymousClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

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

        var html = await factory.CreateClientAs("analyst", "analyst").GetStringAsync("/");

        Assert.Contains(title, html);
    }
}
