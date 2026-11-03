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
    public async Task Index_is_forbidden_for_user_without_role()
    {
        var response = await factory.CreateClientAs("no-role-user").GetAsync("/");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Index_lists_saved_findings()
    {
        var title = await AddFinding(assigneeId: null);

        var html = await factory.CreateClientAs("analyst", "analyst").GetStringAsync("/");

        Assert.Contains(title, html);
    }

    [Fact]
    public async Task Index_shows_developer_only_their_assigned_findings()
    {
        var developerId = $"dev-{Guid.NewGuid()}";
        var assigned = await AddFinding(assigneeId: developerId);
        var notAssigned = await AddFinding(assigneeId: null);

        var html = await factory.CreateClientAs(developerId, "developer").GetStringAsync("/");

        Assert.Contains(assigned, html);
        Assert.DoesNotContain(notAssigned, html);
    }

    // Returns the unique title of the saved finding.
    private async Task<string> AddFinding(string? assigneeId)
    {
        var title = $"SQL injection in login {Guid.NewGuid()}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Findings.Add(new Finding
        {
            Title = title,
            Description = "Login form concatenates input into SQL.",
            AffectedAsset = "auth-service",
            Severity = Severity.High,
            DiscoveryDate = new DateOnly(2026, 11, 3),
            Source = FindingSource.Internal,
            AssigneeId = assigneeId,
        });
        await db.SaveChangesAsync();
        return title;
    }
}
