namespace VulnTracker.Authorization;

// Keycloak realm roles (spec §2). External reporters are not logged in, so they have no role.
public static class Roles
{
    public const string Analyst = "analyst";
    public const string Developer = "developer";
    public const string Pm = "pm";
    public const string Auditor = "auditor";

    public static readonly string[] All = [Analyst, Developer, Pm, Auditor];
}
