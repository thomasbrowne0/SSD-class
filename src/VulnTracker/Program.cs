using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using VulnTracker.Authorization;
using VulnTracker.Data;
using VulnTracker.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
// FIND-11: keys come from configuration (environment variables in production), never the database.
builder.Services.AddSingleton(sp => FieldEncryptor.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
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

// Keycloak's origin as the browser sees it. Only differs from Oidc:Authority when the app reaches
// Keycloak by another host name (docker compose).
var keycloakOrigin = new Uri(app.Configuration["Oidc:BrowserAuthority"] ?? app.Configuration["Oidc:Authority"]!)
    .GetLeftPart(UriPartial.Authority);
// Only load resources from this site; no framing (clickjacking), no plugins.
// Forms may only submit here, or redirect to Keycloak (logout).
var contentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'; "
    + $"form-action 'self' {keycloakOrigin}";

// Security headers that the DAST scan (ZAP baseline) reported missing.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.ContentSecurityPolicy = contentSecurityPolicy;
    headers.XContentTypeOptions = "nosniff";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    headers["Cross-Origin-Opener-Policy"] = "same-origin";
    headers["Cross-Origin-Embedder-Policy"] = "require-corp";
    headers["Cross-Origin-Resource-Policy"] = "same-origin";
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();

// Lets the test project use WebApplicationFactory<Program>.
public partial class Program;
