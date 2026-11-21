# Access control (AUTH-02)

## What is asked

AUTH-02 (MUST): proper access control according to the user roles in the spec. W41 adds that plain
role-based access control (RBAC) is usually not enough for least privilege, and suggests combining it with
attribute-based (ABAC) or relationship-based (ReBAC) rules. W41 also asks for the rules to be verified,
preferably with automated tests.

## How it works

Three layers:

1. **Fallback policy:** every page requires login unless it is marked `[AllowAnonymous]`. Forgetting to protect a new
   page fails safe.
2. **Role policies (RBAC):** named policies like `CreateFinding` list which roles may do something. Pages say which
   policy they need with `[Authorize(Policy = ...)]`.
3. **Per-finding rules (ReBAC and ABAC):** some rules depend on the finding itself, so they can only be checked after
   the finding is loaded:
   - ReBAC: a developer may only view and edit findings **assigned to them** (relationship between user and finding).
   - ABAC: only analysts and PMs may set the status to *Accepted Risk* (depends on an attribute of the change).

The same "who can see what" rule is also applied to database queries, so lists and counts never include findings
the user may not open.

## Walk through the code

**src/VulnTracker/Authorization/Roles.cs**: the four Keycloak role names (lines 6-9).

**src/VulnTracker/Authorization/Policies.cs**

- Line 19: registers the per-finding handler.
- Line 23: the fallback policy (login required by default).
- Line 25: `ViewFindings` requires one of our four roles. A Keycloak user with no role gets nothing.
- Lines 26-32: the role table from the spec. Some policies (SLA targets, roles, audit log, disclosure) are defined
  but no page uses them yet, because those features were SHOULDs.

**src/VulnTracker/Authorization/FindingAuthorization.cs**

- Lines 10-15: the three operations on a single finding: `View`, `Update`, `AcceptRisk`.
- Lines 20-45: the handler. Line 31: view is allowed for analyst, PM, auditor, or the assigned developer.
  Line 33: update for analyst, PM, or the assigned developer. Line 34: accept risk only for analyst and PM.
- Lines 50-64: `VisibleTo` applies the view rule to a query. Developers get a `WHERE AssigneeId = <their id>`
  (line 60). Users with no role get nothing (line 63).
- Line 70: the user id is the `sub` claim from Keycloak.

**Where the rules are used**

- `Pages/Findings/Details.cshtml.cs` line 27: loads the finding, then checks `View`. Returns 403 if not allowed.
- `Pages/Findings/Edit.cshtml.cs` lines 54 and 59-63: checks `Update`, and checks `AcceptRisk` separately when the
  status is changed to Accepted Risk. The dropdown also hides the option (lines 87-91), but the server check is
  what actually protects it, because a form can be edited by hand.
- `Pages/Index.cshtml.cs` line 34 and `Pages/Dashboard.cshtml.cs` line 26: use `VisibleTo`.

## How it's tested

**tests/VulnTracker.Tests/AuthorizationTests.cs**

- Line 49, `Policy_allows_only_the_spec_roles`: every policy against every role plus a user with no role. The expected
  roles are written out again in the test (not copied from the code), so a mistake in the code shows up as a failure.
- Line 75, `Finding_operation_rules`: view, update and accept risk for each role, including an assigned and a
  non-assigned developer.
- Line 99, `VisibleTo_filters_findings_list`: the query filter.

The page tests in `FindingFormsTests.cs` check the same rules through real HTTP requests, for example
`Unassigned_developer_cannot_open_finding` (line 80) and `Developer_cannot_set_accepted_risk` (line 124).

To check that the tests really catch mistakes, we broke two rules on purpose (let developers create findings, and
removed the "assigned to me" check). Four tests failed, then the code was restored.

## Try it yourself

1. Log in as `analyst`, create a finding, and assign it to nobody.
2. Log in as `developer` and open `/Findings/Details/<id>` for that finding. You get "Access denied".
3. As `analyst`, assign it to `developer`. Now the developer can open it, but the status dropdown has no AcceptedRisk.

```sh
dotnet test --filter AuthorizationTests
```

## Questions you might get

**What is RBAC, ABAC and ReBAC?** RBAC decides by role. ABAC decides by attributes of the user, resource or request.
ReBAC decides by the relationship between user and resource. We use all three.

**What is IDOR and how do you prevent it?** Insecure direct object reference: changing an id in the URL to see
someone else's data. We check access per finding after loading it, not just "is logged in".

**Why check on the server if the dropdown already hides Accepted Risk?** The browser is controlled by the user.
Anyone can send a request with any value (Burp Suite makes this easy). Only server-side checks count.

**Why a fallback policy?** Deny by default. A new page is protected even if someone forgets the attribute.

**Why does a user with no role get nothing?** Anyone can have a Keycloak account in the company. Having an account
shouldn't give access to a list of unpatched vulnerabilities.
