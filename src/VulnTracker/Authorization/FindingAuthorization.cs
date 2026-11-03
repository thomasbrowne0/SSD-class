using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using VulnTracker.Data;

namespace VulnTracker.Authorization;

// Resource-based operations on a single finding. Check with:
// authorizationService.AuthorizeAsync(User, finding, FindingOperations.Update)
public static class FindingOperations
{
    public static readonly OperationAuthorizationRequirement View = new() { Name = nameof(View) };
    public static readonly OperationAuthorizationRequirement Update = new() { Name = nameof(Update) };
    public static readonly OperationAuthorizationRequirement AcceptRisk = new() { Name = nameof(AcceptRisk) };
}

// Rules that RBAC alone can't express (least privilege):
// - ReBAC: a Developer can only view and update findings assigned to them.
// - ABAC: only Analyst and PM can set status to Accepted Risk, even though Developers can change status.
public class FindingAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, Finding>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OperationAuthorizationRequirement requirement, Finding finding)
    {
        var user = context.User;
        var isAnalystOrPm = user.IsInRole(Roles.Analyst) || user.IsInRole(Roles.Pm);

        var allowed = requirement.Name switch
        {
            nameof(FindingOperations.View) =>
                isAnalystOrPm || user.IsInRole(Roles.Auditor) || FindingAccess.IsAssignedDeveloper(user, finding),
            nameof(FindingOperations.Update) =>
                isAnalystOrPm || FindingAccess.IsAssignedDeveloper(user, finding),
            nameof(FindingOperations.AcceptRisk) => isAnalystOrPm,
            _ => false,
        };

        if (allowed)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public static class FindingAccess
{
    // Same rule as FindingOperations.View, applied to a database query so lists only contain allowed findings.
    public static IQueryable<Finding> VisibleTo(this IQueryable<Finding> findings, ClaimsPrincipal user)
    {
        if (user.IsInRole(Roles.Analyst) || user.IsInRole(Roles.Pm) || user.IsInRole(Roles.Auditor))
        {
            return findings;
        }

        if (user.IsInRole(Roles.Developer))
        {
            var userId = UserId(user);
            return findings.Where(f => f.AssigneeId != null && f.AssigneeId == userId);
        }

        return findings.Where(_ => false);
    }

    public static bool IsAssignedDeveloper(ClaimsPrincipal user, Finding finding) =>
        user.IsInRole(Roles.Developer) && finding.AssigneeId != null && finding.AssigneeId == UserId(user);

    // Keycloak user id.
    private static string? UserId(ClaimsPrincipal user) => user.FindFirstValue("sub");
}
