using System.Net;
using VulnTracker.Data;

namespace VulnTracker.Tests;

// AUTH-03 / FIND-02: public reporting form.
[Collection(AppCollection.Name)]
public class ReportPageTests(TestAppFactory factory)
{
    private static Dictionary<string, string> ReportFields(string title, string email = "researcher@example.test") => new()
    {
        ["Input.Title"] = title,
        ["Input.Description"] = "The /search endpoint reflects the q parameter unencoded.",
        ["Input.AffectedAsset"] = "public-website",
        ["Input.Severity"] = nameof(Severity.Medium),
        ["Input.ContactEmail"] = email,
    };

    [Fact]
    public async Task Anonymous_user_can_open_form()
    {
        var response = await factory.CreateAnonymousClient().GetAsync("/Report");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_report_creates_external_finding_and_acknowledges()
    {
        var client = factory.CreateAnonymousClient();
        var title = TestAppFactory.UniqueId("Reflected XSS in search");

        var response = await client.SubmitFormAsync("/Report", ReportFields(title));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var acknowledgement = await client.GetStringAsync(response.Headers.Location!.ToString());
        Assert.Contains("Thank you. We have received your report.", acknowledgement);

        var saved = await factory.FindByTitle(title);
        Assert.NotNull(saved);
        Assert.Equal(FindingSource.External, saved.Source);
        Assert.Equal(FindingStatus.New, saved.Status);
        Assert.Equal("researcher@example.test", saved.ReporterEmail);
        Assert.Null(saved.AssigneeId);
    }

    [Fact]
    public async Task Report_requires_valid_email()
    {
        var title = TestAppFactory.UniqueId("No email");

        var response = await factory.CreateAnonymousClient()
            .SubmitFormAsync("/Report", ReportFields(title, email: "not-an-email"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await factory.FindByTitle(title));
    }

    [Fact]
    public async Task Report_ignores_fields_that_are_not_on_the_form()
    {
        var title = TestAppFactory.UniqueId("Mass assignment attempt");
        var fields = ReportFields(title);
        fields["Input.Status"] = nameof(FindingStatus.AcceptedRisk);
        fields["Input.Source"] = nameof(FindingSource.Internal);
        fields["Input.AssigneeId"] = "attacker";

        await factory.CreateAnonymousClient().SubmitFormAsync("/Report", fields);

        var saved = await factory.FindByTitle(title);
        Assert.Equal(FindingStatus.New, saved!.Status);
        Assert.Equal(FindingSource.External, saved.Source);
        Assert.Null(saved.AssigneeId);
    }

    [Fact]
    public async Task Anonymous_user_cannot_read_findings()
    {
        var finding = await factory.AddFinding();
        var client = factory.CreateAnonymousClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/Dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/Findings/Details/{finding.Id}")).StatusCode);
    }
}
