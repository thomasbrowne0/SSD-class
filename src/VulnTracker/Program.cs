using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using VulnTracker.Authorization;
using VulnTracker.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// AUTH-01: login is delegated to Keycloak with OpenID Connect. The app keeps the session in a cookie.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options => options.AccessDeniedPath = "/AccessDenied")
    .AddOpenIdConnect(options =>
    {
        var oidc = builder.Configuration.GetSection("Oidc");
        options.Authority = oidc["Authority"];
        options.ClientId = oidc["ClientId"];
        options.ClientSecret = oidc["ClientSecret"];
        options.ResponseType = OpenIdConnectResponseType.Code;
        // Keeps the id_token so logout can also end the Keycloak session.
        options.SaveTokens = true;
        // Keep claim names as Keycloak sends them, and read roles from the "roles" claim (realm role mapper).
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "preferred_username";
        options.TokenValidationParameters.RoleClaimType = "roles";
        // Local Keycloak runs on plain http.
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        // Remember who has logged in, so they can be picked as assignee.
        options.Events.OnTokenValidated = context => AppUser.SaveLoginAsync(
            context.HttpContext.RequestServices.GetRequiredService<AppDbContext>(), context.Principal!);
    });

// AUTH-02: access control rules are in the Authorization folder.
builder.Services.AddVulnTrackerAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();

// Lets the test project use WebApplicationFactory<Program>.
public partial class Program;
