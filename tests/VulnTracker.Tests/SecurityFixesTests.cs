using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace VulnTracker.Tests;

// Fixes for the medium findings from the security audit.
[Collection(AppCollection.Name)]
public class SecurityFixesTests(TestAppFactory factory)
{
    [Fact]
    public void Session_is_short_and_not_renewed_so_keycloak_changes_take_effect()
    {
        var cookie = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Equal(TimeSpan.FromMinutes(30), cookie.ExpireTimeSpan);
        Assert.False(cookie.SlidingExpiration);
    }

    [Fact]
    public async Task Unknown_host_header_is_rejected()
    {
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Host = "evil.example";

        var response = await client.GetAsync("/Report");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Report_form_is_rate_limited()
    {
        // Own app instance with a low limit, so the shared one isn't affected.
        using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimiting:ReportsPerWindow"] = "2" })));
        var client = limited.CreateClient(new() { AllowAutoRedirect = false });

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            var response = await client.SubmitFormAsync("/Report", new()
            {
                ["Input.Title"] = TestAppFactory.UniqueId("Spam"),
                ["Input.Description"] = "Flooding the form.",
                ["Input.AffectedAsset"] = "public-website",
                ["Input.Severity"] = "Low",
                ["Input.ContactEmail"] = "spam@example.test",
            });
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.Redirect, HttpStatusCode.Redirect, HttpStatusCode.TooManyRequests], statuses);
        // Only submissions are limited. The form itself still loads.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Report")).StatusCode);
    }
}
