using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VulnTracker.Data;

namespace VulnTracker.Tests;

// All test classes that use the app share one instance, so they don't race to create the test database.
[CollectionDefinition(Name)]
public class AppCollection : ICollectionFixture<TestAppFactory>
{
    public const string Name = "App";
}

// Uses a separate database so tests don't touch dev data. Needs Postgres running (docker compose up -d db).
// Replaces Keycloak login with a fake scheme, so tests can pick the user and roles per request.
public class TestAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] =
                "Host=localhost;Port=5432;Database=vulntracker_test;Username=postgres;Password=postgres",
            // All test requests share one IP, so the report form limit is raised here.
            // SecurityFixesTests checks the limit itself.
            ["RateLimiting:ReportsPerWindow"] = "1000",
        }));

        builder.ConfigureTestServices(services => services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null));
    }

    // Client that acts as a logged-in user with the given Keycloak roles.
    public HttpClient CreateClientAs(string username, params string[] roles)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, username);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
        return client;
    }

    public HttpClient CreateAnonymousClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // Unique id so tests sharing the database don't see each other's data.
    public static string UniqueId(string prefix) => $"{prefix}-{Guid.NewGuid()}";

    public async Task<AppUser> AddUser(string id)
    {
        await using var db = OpenDb();
        var user = new AppUser { Id = id, Username = id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<Finding> AddFinding(
        string? assigneeId = null,
        Severity severity = Severity.High,
        string? asset = null,
        FindingStatus status = FindingStatus.New)
    {
        await using var db = OpenDb();
        if (assigneeId is not null && await db.Users.FindAsync(assigneeId) is null)
        {
            db.Users.Add(new AppUser { Id = assigneeId, Username = assigneeId });
        }
        var finding = new Finding
        {
            Title = UniqueId("SQL injection in login"),
            Description = "Login form concatenates input into SQL.",
            AffectedAsset = asset ?? "auth-service",
            Severity = severity,
            DiscoveryDate = new DateOnly(2026, 11, 3),
            Source = FindingSource.Internal,
            Status = status,
            AssigneeId = assigneeId,
        };
        db.Findings.Add(finding);
        await db.SaveChangesAsync();
        return finding;
    }

    public async Task<Finding?> GetFinding(int id)
    {
        await using var db = OpenDb();
        return await db.Findings.FindAsync(id);
    }

    // Title is encrypted, so it can't be searched in SQL. Loading everything is fine for test data.
    public async Task<Finding?> FindByTitle(string title)
    {
        await using var db = OpenDb();
        return (await db.Findings.ToListAsync()).SingleOrDefault(f => f.Title == title);
    }

    // The value exactly as stored in the database, without decryption.
    public async Task<string?> RawColumn(int findingId, string column)
    {
        await using var db = OpenDb();
        // Column names can't be SQL parameters. Here they only come from test code, never from user input.
#pragma warning disable EF1002
        return await db.Database
            .SqlQueryRaw<string?>($"SELECT \"{column}\" AS \"Value\" FROM \"Findings\" WHERE \"Id\" = {{0}}", findingId)
            .SingleAsync();
#pragma warning restore EF1002
    }

    private AppDbContext OpenDb() => Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
}

public static class FormExtensions
{
    // Razor Pages require an antiforgery token on POST, so load the form first and send its token back.
    public static async Task<HttpResponseMessage> SubmitFormAsync(
        this HttpClient client, string url, Dictionary<string, string> fields)
    {
        var html = await client.GetStringAsync(url);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(token.Success, "No antiforgery token in form");
        fields["__RequestVerificationToken"] = token.Groups[1].Value;
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }
}

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "Test-User";
    public const string RolesHeader = "Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var username))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Same claim names as the real Keycloak tokens.
        var claims = new List<Claim> { new("sub", username!), new("preferred_username", username!) };
        var roles = Request.Headers[RolesHeader].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        claims.AddRange(roles.Select(role => new Claim("roles", role)));

        var identity = new ClaimsIdentity(claims, SchemeName, "preferred_username", "roles");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
