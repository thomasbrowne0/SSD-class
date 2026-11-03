using System.Net;
using System.Text.RegularExpressions;
using VulnTracker.Data;

namespace VulnTracker.Tests;

// FR-RPT-01: open findings by severity and by status.
[Collection(AppCollection.Name)]
public class DashboardPageTests(TestAppFactory factory)
{
    // Reads the count next to a label in one of the dashboard tables.
    private static int CountFor(string html, string tableId, string label)
    {
        var table = Regex.Match(html, $"<table id=\"{tableId}\">.*?</table>", RegexOptions.Singleline).Value;
        var row = Regex.Match(table, $">{label}</a></td>\\s*<td>(\\d+)</td>");
        Assert.True(row.Success, $"No row for {label} in {tableId}");
        return int.Parse(row.Groups[1].Value);
    }

    [Fact]
    public async Task Dashboard_counts_only_open_findings_the_user_can_see()
    {
        // A Developer only sees findings assigned to them, so the counts are predictable.
        var developerId = TestAppFactory.UniqueId("dev");
        await factory.AddFinding(assigneeId: developerId, severity: Severity.High, status: FindingStatus.New);
        await factory.AddFinding(assigneeId: developerId, severity: Severity.High, status: FindingStatus.InProgress);
        await factory.AddFinding(assigneeId: developerId, severity: Severity.Low, status: FindingStatus.Triaged);
        // Closed, so not counted.
        await factory.AddFinding(assigneeId: developerId, severity: Severity.Critical, status: FindingStatus.Remediated);
        await factory.AddFinding(assigneeId: developerId, severity: Severity.Critical, status: FindingStatus.AcceptedRisk);
        // Someone else's, so not visible.
        await factory.AddFinding(assigneeId: TestAppFactory.UniqueId("dev-other"), severity: Severity.High);

        var html = await factory.CreateClientAs(developerId, "developer").GetStringAsync("/Dashboard");

        Assert.Contains("<strong id=\"total-open\">3</strong>", html);
        Assert.Equal(0, CountFor(html, "by-severity", "Critical"));
        Assert.Equal(2, CountFor(html, "by-severity", "High"));
        Assert.Equal(0, CountFor(html, "by-severity", "Medium"));
        Assert.Equal(1, CountFor(html, "by-severity", "Low"));
        Assert.Equal(1, CountFor(html, "by-status", "New"));
        Assert.Equal(1, CountFor(html, "by-status", "Triaged"));
        Assert.Equal(1, CountFor(html, "by-status", "InProgress"));
    }

    [Theory]
    [InlineData("analyst")]
    [InlineData("developer")]
    [InlineData("pm")]
    [InlineData("auditor")]
    public async Task Every_role_can_open_dashboard(string role)
    {
        var response = await factory.CreateClientAs(role, role).GetAsync("/Dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task User_without_role_cannot_open_dashboard()
    {
        var response = await factory.CreateClientAs("no-role-user").GetAsync("/Dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
