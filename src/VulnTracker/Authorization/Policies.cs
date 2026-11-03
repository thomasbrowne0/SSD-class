using Microsoft.AspNetCore.Authorization;

namespace VulnTracker.Authorization;

// Role-based rules from the spec's role table (§2). Rules that depend on the finding itself
// are in FindingAuthorizationHandler.
public static class Policies
{
    public const string ViewFindings = nameof(ViewFindings);
    public const string CreateFinding = nameof(CreateFinding);
    public const string AssignFinding = nameof(AssignFinding);
    public const string SetSlaTargets = nameof(SetSlaTargets);
    public const string ManageUserRoles = nameof(ManageUserRoles);
    public const string ReadAuditLog = nameof(ReadAuditLog);
    public const string ApproveDisclosure = nameof(ApproveDisclosure);

    public static IServiceCollection AddVulnTrackerAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, FindingAuthorizationHandler>();

        return services.AddAuthorizationBuilder()
            // Every page requires login unless it is marked [AllowAnonymous].
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            // A logged-in Keycloak user without one of our roles gets nothing.
            .AddPolicy(ViewFindings, p => p.RequireRole(Roles.All))
            .AddPolicy(CreateFinding, p => p.RequireRole(Roles.Analyst, Roles.Pm))      // FIND-01
            .AddPolicy(AssignFinding, p => p.RequireRole(Roles.Analyst, Roles.Pm))      // FIND-04
            .AddPolicy(SetSlaTargets, p => p.RequireRole(Roles.Pm))
            .AddPolicy(ManageUserRoles, p => p.RequireRole(Roles.Pm))                   // AUTH-04
            .AddPolicy(ReadAuditLog, p => p.RequireRole(Roles.Pm, Roles.Auditor))       // AUDIT-02
            // The spec says Analyst and Developer can't approve disclosures, but not who can. Our decision: PM.
            .AddPolicy(ApproveDisclosure, p => p.RequireRole(Roles.Pm))
            .Services;
    }
}
