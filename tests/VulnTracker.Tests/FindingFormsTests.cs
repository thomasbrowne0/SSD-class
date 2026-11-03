using System.Net;
using VulnTracker.Data;

namespace VulnTracker.Tests;

// Create, details, edit and assign pages, checked per role.
[Collection(AppCollection.Name)]
public class FindingFormsTests(TestAppFactory factory)
{
    private static Dictionary<string, string> FindingFields(string title, string? status = null)
    {
        var fields = new Dictionary<string, string>
        {
            ["Input.Title"] = title,
            ["Input.Description"] = "Stack trace shown to users.",
            ["Input.AffectedAsset"] = "web-frontend",
            ["Input.Severity"] = nameof(Severity.Medium),
            ["Input.DiscoveryDate"] = "2026-11-02",
        };
        if (status is not null)
        {
            fields["Input.Status"] = status;
        }
        return fields;
    }

    // --- Create (FIND-01) ---

    [Theory]
    [InlineData("analyst")]
    [InlineData("pm")]
    public async Task Analyst_and_pm_can_create_finding(string role)
    {
        var title = TestAppFactory.UniqueId("Verbose errors");

        var response = await factory.CreateClientAs(role, role).SubmitFormAsync("/Findings/Create", FindingFields(title));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var id = int.Parse(response.Headers.Location!.ToString().Split('/').Last());
        var saved = await factory.GetFinding(id);
        Assert.Equal(title, saved!.Title);
        Assert.Equal(FindingStatus.New, saved.Status);
        Assert.Equal(FindingSource.Internal, saved.Source);
    }

    [Theory]
    [InlineData("developer")]
    [InlineData("auditor")]
    public async Task Developer_and_auditor_cannot_open_create_form(string role)
    {
        var response = await factory.CreateClientAs(role, role).GetAsync("/Findings/Create");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_missing_title()
    {
        var response = await factory.CreateClientAs("analyst", "analyst")
            .SubmitFormAsync("/Findings/Create", FindingFields(title: ""));

        // Form is shown again instead of redirecting to the new finding.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_undefined_severity()
    {
        var fields = FindingFields(TestAppFactory.UniqueId("Bad severity"));
        fields["Input.Severity"] = "99";

        var response = await factory.CreateClientAs("analyst", "analyst").SubmitFormAsync("/Findings/Create", fields);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Details ---

    [Fact]
    public async Task Unassigned_developer_cannot_open_finding()
    {
        var finding = await factory.AddFinding(assigneeId: TestAppFactory.UniqueId("dev-other"));

        var response = await factory.CreateClientAs("dev-me", "developer").GetAsync($"/Findings/Details/{finding.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Auditor_can_view_but_not_edit_or_assign()
    {
        var finding = await factory.AddFinding();
        var client = factory.CreateClientAs("auditor", "auditor");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Findings/Details/{finding.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/Findings/Edit/{finding.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/Findings/Assign/{finding.Id}")).StatusCode);
    }

    [Fact]
    public async Task Missing_finding_returns_not_found()
    {
        var response = await factory.CreateClientAs("analyst", "analyst").GetAsync("/Findings/Details/999999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Edit details / status ---

    [Fact]
    public async Task Assigned_developer_can_update_status()
    {
        var developerId = TestAppFactory.UniqueId("dev");
        var finding = await factory.AddFinding(assigneeId: developerId);

        var response = await factory.CreateClientAs(developerId, "developer")
            .SubmitFormAsync($"/Findings/Edit/{finding.Id}", FindingFields(finding.Title, nameof(FindingStatus.InProgress)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(FindingStatus.InProgress, (await factory.GetFinding(finding.Id))!.Status);
    }

    [Fact]
    public async Task Developer_cannot_set_accepted_risk()
    {
        var developerId = TestAppFactory.UniqueId("dev");
        var finding = await factory.AddFinding(assigneeId: developerId);

        var response = await factory.CreateClientAs(developerId, "developer")
            .SubmitFormAsync($"/Findings/Edit/{finding.Id}", FindingFields(finding.Title, nameof(FindingStatus.AcceptedRisk)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(FindingStatus.New, (await factory.GetFinding(finding.Id))!.Status);
    }

    [Fact]
    public async Task Analyst_can_set_accepted_risk()
    {
        var finding = await factory.AddFinding();

        var response = await factory.CreateClientAs("analyst", "analyst")
            .SubmitFormAsync($"/Findings/Edit/{finding.Id}", FindingFields(finding.Title, nameof(FindingStatus.AcceptedRisk)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(FindingStatus.AcceptedRisk, (await factory.GetFinding(finding.Id))!.Status);
    }

    [Fact]
    public async Task Edit_ignores_fields_that_are_not_on_the_form()
    {
        var finding = await factory.AddFinding();
        var fields = FindingFields(finding.Title, nameof(FindingStatus.Triaged));
        // Extra fields an attacker might add by hand (mass assignment).
        fields["Input.AssigneeId"] = "attacker";
        fields["Input.Source"] = nameof(FindingSource.External);
        fields["AssigneeId"] = "attacker";

        await factory.CreateClientAs("analyst", "analyst").SubmitFormAsync($"/Findings/Edit/{finding.Id}", fields);

        var saved = await factory.GetFinding(finding.Id);
        Assert.Null(saved!.AssigneeId);
        Assert.Equal(FindingSource.Internal, saved.Source);
    }

    // --- Assign (FIND-04) ---

    [Fact]
    public async Task Analyst_can_assign_finding_with_due_date()
    {
        var finding = await factory.AddFinding();
        var developer = await factory.AddUser(TestAppFactory.UniqueId("dev"));

        var response = await factory.CreateClientAs("analyst", "analyst").SubmitFormAsync(
            $"/Findings/Assign/{finding.Id}",
            new() { ["Input.AssigneeId"] = developer.Id, ["Input.DueDate"] = "2026-11-30" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = await factory.GetFinding(finding.Id);
        Assert.Equal(developer.Id, saved!.AssigneeId);
        Assert.Equal(new DateOnly(2026, 11, 30), saved.DueDate);
    }

    [Fact]
    public async Task Assign_rejects_unknown_user()
    {
        var finding = await factory.AddFinding();

        var response = await factory.CreateClientAs("pm", "pm").SubmitFormAsync(
            $"/Findings/Assign/{finding.Id}", new() { ["Input.AssigneeId"] = "does-not-exist" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await factory.GetFinding(finding.Id))!.AssigneeId);
    }

    [Fact]
    public async Task Assigned_developer_cannot_reassign()
    {
        var developerId = TestAppFactory.UniqueId("dev");
        var finding = await factory.AddFinding(assigneeId: developerId);

        var response = await factory.CreateClientAs(developerId, "developer").GetAsync($"/Findings/Assign/{finding.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
