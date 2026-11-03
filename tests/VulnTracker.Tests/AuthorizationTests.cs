using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using VulnTracker.Authorization;
using VulnTracker.Data;

namespace VulnTracker.Tests;

// Checks the access control rules directly (no web server or database needed).
public class AuthorizationTests
{
    private const string NoRole = "";

    private readonly IAuthorizationService authorization = new ServiceCollection()
        .AddLogging()
        .AddVulnTrackerAuthorization()
        .BuildServiceProvider()
        .GetRequiredService<IAuthorizationService>();

    // The spec's role table (§2), written out separately from the implementation on purpose.
    private static readonly Dictionary<string, string[]> AllowedRoles = new()
    {
        [Policies.ViewFindings] = [Roles.Analyst, Roles.Developer, Roles.Pm, Roles.Auditor],
        [Policies.CreateFinding] = [Roles.Analyst, Roles.Pm],
        [Policies.AssignFinding] = [Roles.Analyst, Roles.Pm],
        [Policies.SetSlaTargets] = [Roles.Pm],
        [Policies.ManageUserRoles] = [Roles.Pm],
        [Policies.ReadAuditLog] = [Roles.Pm, Roles.Auditor],
        [Policies.ApproveDisclosure] = [Roles.Pm],
    };

    // Every policy × every role (plus a user with no role).
    public static TheoryData<string, string, bool> PolicyCases()
    {
        var cases = new TheoryData<string, string, bool>();
        foreach (var (policy, allowed) in AllowedRoles)
        {
            foreach (var role in Roles.All.Append(NoRole))
            {
                cases.Add(policy, role, allowed.Contains(role));
            }
        }
        return cases;
    }

    [Theory]
    [MemberData(nameof(PolicyCases))]
    public async Task Policy_allows_only_the_spec_roles(string policy, string role, bool expected)
    {
        var result = await authorization.AuthorizeAsync(User(role), policy);

        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    // View
    [InlineData(nameof(FindingOperations.View), Roles.Analyst, "analyst-1", true)]
    [InlineData(nameof(FindingOperations.View), Roles.Pm, "pm-1", true)]
    [InlineData(nameof(FindingOperations.View), Roles.Auditor, "auditor-1", true)]
    [InlineData(nameof(FindingOperations.View), Roles.Developer, "dev-assigned", true)]
    [InlineData(nameof(FindingOperations.View), Roles.Developer, "dev-other", false)]
    [InlineData(nameof(FindingOperations.View), NoRole, "dev-assigned", false)]
    // Update
    [InlineData(nameof(FindingOperations.Update), Roles.Analyst, "analyst-1", true)]
    [InlineData(nameof(FindingOperations.Update), Roles.Pm, "pm-1", true)]
    [InlineData(nameof(FindingOperations.Update), Roles.Auditor, "auditor-1", false)]
    [InlineData(nameof(FindingOperations.Update), Roles.Developer, "dev-assigned", true)]
    [InlineData(nameof(FindingOperations.Update), Roles.Developer, "dev-other", false)]
    // Accept risk
    [InlineData(nameof(FindingOperations.AcceptRisk), Roles.Analyst, "analyst-1", true)]
    [InlineData(nameof(FindingOperations.AcceptRisk), Roles.Pm, "pm-1", true)]
    [InlineData(nameof(FindingOperations.AcceptRisk), Roles.Auditor, "auditor-1", false)]
    [InlineData(nameof(FindingOperations.AcceptRisk), Roles.Developer, "dev-assigned", false)]
    public async Task Finding_operation_rules(string operation, string role, string userId, bool expected)
    {
        var finding = NewFinding(assigneeId: "dev-assigned");

        var result = await authorization.AuthorizeAsync(User(role, userId), finding, Operation(operation));

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task Developer_cannot_view_unassigned_finding()
    {
        var result = await authorization.AuthorizeAsync(
            User(Roles.Developer, "dev-1"), NewFinding(assigneeId: null), FindingOperations.View);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(Roles.Analyst, "analyst-1", 3)]
    [InlineData(Roles.Pm, "pm-1", 3)]
    [InlineData(Roles.Auditor, "auditor-1", 3)]
    [InlineData(Roles.Developer, "dev-1", 1)]
    [InlineData(NoRole, "dev-1", 0)]
    public void VisibleTo_filters_findings_list(string role, string userId, int expectedCount)
    {
        var findings = new[]
        {
            NewFinding(assigneeId: "dev-1"),
            NewFinding(assigneeId: "dev-2"),
            NewFinding(assigneeId: null),
        }.AsQueryable();

        var visible = findings.VisibleTo(User(role, userId)).ToList();

        Assert.Equal(expectedCount, visible.Count);
    }

    private static ClaimsPrincipal User(string role, string userId = "user-1")
    {
        var claims = new List<Claim> { new("sub", userId) };
        if (role != NoRole)
        {
            claims.Add(new Claim("roles", role));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", "sub", "roles"));
    }

    private static OperationAuthorizationRequirement Operation(string name) => name switch
    {
        nameof(FindingOperations.View) => FindingOperations.View,
        nameof(FindingOperations.Update) => FindingOperations.Update,
        nameof(FindingOperations.AcceptRisk) => FindingOperations.AcceptRisk,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static Finding NewFinding(string? assigneeId) => new()
    {
        Title = "XSS in comments",
        Description = "Comment body is rendered without encoding.",
        AffectedAsset = "web-frontend",
        Severity = Severity.Medium,
        DiscoveryDate = new DateOnly(2026, 11, 3),
        Source = FindingSource.Internal,
        AssigneeId = assigneeId,
    };
}
