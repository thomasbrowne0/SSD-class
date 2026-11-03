using System.Net;
using VulnTracker.Data;

namespace VulnTracker.Tests;

// The findings list (Index page).
[Collection(AppCollection.Name)]
public class FindingsPageTests(TestAppFactory factory)
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
        var finding = await factory.AddFinding();

        var html = await factory.CreateClientAs("analyst", "analyst").GetStringAsync("/");

        Assert.Contains(finding.Title, html);
    }

    [Fact]
    public async Task Index_shows_developer_only_their_assigned_findings()
    {
        var developerId = TestAppFactory.UniqueId("dev");
        var assigned = await factory.AddFinding(assigneeId: developerId);
        var notAssigned = await factory.AddFinding();

        var html = await factory.CreateClientAs(developerId, "developer").GetStringAsync("/");

        Assert.Contains(assigned.Title, html);
        Assert.DoesNotContain(notAssigned.Title, html);
    }

    [Fact]
    public async Task Index_filters_by_severity_and_asset()
    {
        var asset = TestAppFactory.UniqueId("payments-api");
        var high = await factory.AddFinding(severity: Severity.High, asset: asset);
        var low = await factory.AddFinding(severity: Severity.Low, asset: asset);
        var otherAsset = await factory.AddFinding(severity: Severity.High);

        var html = await factory.CreateClientAs("analyst", "analyst")
            .GetStringAsync($"/?severity=High&asset={asset.ToUpperInvariant()}");

        Assert.Contains(high.Title, html);
        Assert.DoesNotContain(low.Title, html);
        Assert.DoesNotContain(otherAsset.Title, html);
    }

    [Fact]
    public async Task Index_filters_by_status_and_assignee()
    {
        var developerId = TestAppFactory.UniqueId("dev");
        var match = await factory.AddFinding(assigneeId: developerId, status: FindingStatus.InProgress);
        var otherStatus = await factory.AddFinding(assigneeId: developerId, status: FindingStatus.New);
        var unassigned = await factory.AddFinding(status: FindingStatus.InProgress);

        var html = await factory.CreateClientAs("pm", "pm")
            .GetStringAsync($"/?status=InProgress&assigneeId={developerId}");

        Assert.Contains(match.Title, html);
        Assert.DoesNotContain(otherStatus.Title, html);
        Assert.DoesNotContain(unassigned.Title, html);
    }
}
