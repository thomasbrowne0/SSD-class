# Requirements (W38)

VulnTracker is a CRA-focused vulnerability tracker. This document answers the W38 questions:
functional requirements, user roles and what each may do, security requirements and abuse cases.

Source: the exam project spec (`spec.md` on Moodle). Requirement IDs are the spec's.

## 1. Functional requirements

Scope for the solo project: all **MUST** requirements, plus encryption (FIND-11), which the W44 lecture asks for.

| ID | Requirement | Priority | Status | Where |
|---|---|---|---|---|
| AUTH-01 | Authenticate via external IdP using OIDC (Keycloak) | MUST | Built | `Program.cs`, `keycloak/vulntracker-realm.json` |
| AUTH-02 | Proper access control per user roles | MUST | Built | `Authorization/` |
| AUTH-03 | Public vulnerability reporting form with acknowledgement | MUST | Built | `Pages/Report` |
| FIND-01 | Analyst and PM create findings manually | MUST | Built | `Pages/Findings/Create` |
| FIND-02 | External reporter submits finding via form | MUST | Built | `Pages/Report` |
| FIND-04 | Analyst/PM assign finding to a user + due date / SLA | MUST | Built | `Pages/Findings/Assign` |
| FIND-05 | Filter/search by status, severity, assigned owner, affected asset | MUST | Built | `Pages/Index` |
| FR-RPT-01 | Dashboard of open findings by severity and by status | MUST | Built | `Pages/Dashboard` |
| FIND-11 | Encrypt finding details | SHOULD | Built (W44) | `Security/FieldEncryptor.cs` |
| AUTH-04, FIND-03, FIND-06, FIND-07, AUDIT-01, AUDIT-02, FR-RPT-02, FR-RPT-04 | | SHOULD | Not built (solo scope) | |
| AUTH-05, FIND-08/09/10, AUDIT-03, FR-RPT-03/05 | | STRETCH | Not built | |

Finding data (spec §3): title, description, affected asset/component, severity (Critical/High/Medium/Low),
discovery date, status (New, Triaged, In Progress, Remediated, Accepted Risk), and contact email for external reporters.
The tracker also stores the intake channel (Internal/External/Scan), assignee and due date.

## 2. Roles and permissions

Roles are Keycloak realm roles. External reporters are not logged in.

| Action | Analyst | Developer | PM | Auditor | External reporter |
|---|---|---|---|---|---|
| View findings / dashboard | All | **Only assigned to them** | All | All | ✗ |
| Create finding | ✓ | ✗ | ✓ | ✗ | Via public form only |
| Edit details and status | ✓ | **Only assigned to them** | ✓ | ✗ | ✗ |
| Set status *Accepted Risk* | ✓ | ✗ | ✓ | ✗ | ✗ |
| Assign + set due date | ✓ | ✗ | ✓ | ✗ | ✗ |
| Set SLA targets, manage roles | ✗ | ✗ | ✓ (policy defined, no page yet) | ✗ | ✗ |
| Approve public disclosure | ✗ | ✗ | ✓ (policy defined, no page yet) | ✗ | ✗ |
| Read audit log | ✗ | ✗ | ✓ (policy defined, no page yet) | ✓ (policy defined, no page yet) | ✗ |

A logged-in Keycloak user without one of the four roles is denied everything.

**Decisions where the spec is open:**

- **Disclosure approval: PM.** The spec only says Analyst and Developer can't approve.
- **Developers only see and edit findings assigned to them.** This is relationship-based access control (ReBAC). It follows least privilege, since the tracker is a list of unpatched vulnerabilities.
- **Only Analyst and PM can set *Accepted Risk*.** This is attribute-based access control (ABAC). Accepting a risk is a business decision, not a developer's.

## 3. Security requirements

Based on the OWASP Top 10 Proactive Controls (2024), C1-C10.

| Control | Requirement for VulnTracker | Status |
|---|---|---|
| C1 Implement Access Control | Deny by default. Every page requires login unless explicitly public. Checks are done server-side per role and per finding (section 2). Rules are covered by automated tests. | Done: `Authorization/`, `AuthorizationTests.cs` |
| C2 Use Cryptography to Protect Data | Finding title, description and reporter email are encrypted at rest with AES-256-GCM. Keys are kept out of the database and git (production). | Done: [cryptography.md](cryptography.md) |
| C3 Validate all Input & Handle Exceptions | Every form has length limits, required fields and email format checks. Undefined enum values are rejected. Forms bind to input models only (no mass assignment). Output is HTML-encoded by Razor. Queries are parameterized by EF Core. | Done: `Pages/Findings/Inputs.cs`, form tests |
| C4 Address Security from the Start | Security requirements and abuse cases are written from the spec, there is a threat model, and security gates run in CI on every change. | Partly: the threat model was written after most of the code was built, see [llm-usage.md](llm-usage.md). [threat-model.md](threat-model.md), `ci.yml` |
| C5 Secure By Default Configurations | Fallback policy requires login. The container runs as a non-root user. Security headers (CSP, nosniff, etc.) are set. Dev ports are bound to 127.0.0.1. | Done |
| C6 Keep your Components Secure | SCA on NuGet packages, SBOM of the image, Grype scan before push. | Done: `ci.yml` |
| C7 Secure Digital Identities | No passwords in the app. Login is delegated to Keycloak with OIDC authorization code flow, PKCE, client secret and exact redirect URIs. Logout ends the Keycloak session. | Done |
| C8 Leverage Browser Security Features | CSP with `frame-ancestors 'none'` and `form-action`, `X-Content-Type-Options`, `Permissions-Policy`, COOP/COEP/CORP. Antiforgery tokens on all forms. | Done, checked by ZAP |
| C9 Implement Security Logging and Monitoring | Who did what, to which finding, and when. | Not built: the audit log (AUDIT-01) is a SHOULD and outside solo scope. Known gap. |
| C10 Stop Server Side Request Forgery | The app makes no outbound requests based on user input. It only calls the configured Keycloak. | Yes, by design |

## 4. Abuse cases

"As an attacker, I want to … so that …". Each has a mitigation and, where possible, a test.

| # | Abuse case | Mitigation | Test |
|---|---|---|---|
| A1 | As an **anonymous attacker**, I want to read findings through the public form or by guessing URLs, so that I learn unpatched vulnerabilities. | Public form only creates findings. Its acknowledgement shows no finding data. All other pages require login. | `Anonymous_user_cannot_read_findings`, `Index_requires_login` |
| A2 | As a **developer**, I want to open findings not assigned to me, so that I can exploit them or sell them. | ReBAC rule on the details page and on list and dashboard queries. | `Unassigned_developer_cannot_open_finding`, `Index_shows_developer_only_their_assigned_findings`, `Dashboard_counts_only_open_findings_the_user_can_see` |
| A3 | As a **developer**, I want to set my overdue finding to *Accepted Risk*, so that it stops showing as open. | ABAC rule, checked server-side even though the option is hidden in the form. | `Developer_cannot_set_accepted_risk` |
| A4 | As an **external reporter**, I want to add hidden form fields (status, assignee, source), so that my report skips triage or lands on a specific person. | Forms bind to input models with only the allowed fields. | `Report_ignores_fields_that_are_not_on_the_form`, `Edit_ignores_fields_that_are_not_on_the_form` |
| A5 | As an **attacker who stole a database backup**, I want to read all vulnerabilities, so that I can attack the products. | AES-256-GCM encryption of finding text. The key is not in the database. | `Finding_text_fields_are_encrypted_in_the_database` |
| A6 | As an **insider with database access**, I want to change a finding's description, so that the real problem is hidden. | GCM authentication tag: a changed ciphertext fails to decrypt. | `Tampered_value_fails_to_decrypt`, `Value_moved_to_another_column_fails_to_decrypt` |
| A7 | As an **auditor**, I want to edit findings, so that I can cover up a mistake. | Auditor has no write policy. | `Auditor_can_view_but_not_edit_or_assign` |
| A8 | As an **attacker**, I want to trick a logged-in analyst's browser into submitting a form (CSRF), so that findings get closed. | Antiforgery token on every POST. Logout is POST-only. | All form tests post with a token |
| A9 | As an **attacker**, I want to frame the app on my site (clickjacking), so that an analyst clicks "Save" without knowing. | CSP `frame-ancestors 'none'`. | ZAP DAST gate |
| A10 | As an **attacker**, I want to steal the OIDC authorization code with a malicious redirect URI, so that I can log in as the victim. | Keycloak allows only exact redirect URIs. PKCE is required. | Verified manually: Keycloak rejected a wrong host |
| A11 | As an **attacker**, I want to flood the public form, so that real reports drown or the database fills. | **Not mitigated** (no rate limiting). Accepted risk for the exam scope. See threat model. | - |
| A12 | As an **attacker**, I want to slip a malicious package into a dependency, so that my code runs in the app. | SCA in CI, SBOM + Grype scan of the image before push. | CI gates |
