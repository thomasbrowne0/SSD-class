# Create, edit and assign findings (FIND-01, FIND-04)

## What is asked

FIND-01 (MUST): analysts and PMs can create a finding manually.
FIND-04 (MUST): analysts and PMs can assign a finding to a specific user and set a due date / SLA target.
The roles table also says developers can update the status and details of findings, and the spec's statuses
are New, Triaged, In Progress, Remediated and Accepted Risk.

## How it works

Four pages under `/Findings`:

- **Create**: analyst and PM. Saves a new finding with status New and source Internal.
- **Details**: anyone allowed to view that finding. Shows "Edit" and "Assign" links only to users who can use them.
- **Edit**: analyst, PM, or the assigned developer. Changes details and status. Accepted Risk only for analyst and PM.
- **Assign**: analyst and PM. Picks a user and a due date.

Users live in Keycloak, so to pick an assignee the app needs its own list. Every time someone logs in, the app saves
their Keycloak id and username in a `Users` table. That means a user has to log in once before they can be assigned.
Only id and username are stored (data minimisation).

## Walk through the code

**src/VulnTracker/Data/Finding.cs**: the finding with the spec's fields plus source, assignee and due date.

**src/VulnTracker/Data/AppUser.cs**: lines 18-37 save or update the user on every login. Called from
`Program.cs` lines 41-42.

**src/VulnTracker/Pages/Findings/Inputs.cs**: the form models.

- Lines 8-25: `FindingInput` with validation (required, max lengths). Line 20: `EnumDataType` rejects a severity
  like `99` that isn't a defined value.
- Lines 27-31: `EditInput` adds the status.
- Lines 33-40: `AssignInput` with assignee and due date.

**Create** (`Pages/Findings/Create.cshtml.cs`): line 10 requires the `CreateFinding` policy. Lines 27-35 build the
finding, with `Source = Internal` set by the server.

**Details** (`Pages/Findings/Details.cshtml.cs`): line 24 returns 404 if the finding doesn't exist, line 29 returns
403 if the user may not view it. Lines 33-34 decide whether to show the edit and assign links.

**Edit** (`Pages/Findings/Edit.cshtml.cs`): the access check runs on both GET (line 29) and POST (line 54), because
a POST can be sent without ever loading the form. Lines 59-63 are the Accepted Risk rule. Lines 70-75 copy only the
allowed fields onto the finding.

**Assign** (`Pages/Findings/Assign.cshtml.cs`): line 12 requires the `AssignFinding` policy. Lines 44-48 reject an
assignee id that isn't in the `Users` table. An empty choice means unassigned (line 44).

## How it's tested

**tests/VulnTracker.Tests/FindingFormsTests.cs**

- Create: line 32 (analyst and PM can), line 49 (developer and auditor get 403), lines 57 and 67 (validation).
- Details: line 80 (unassigned developer gets 403), line 90 (auditor can view but not edit or assign), line 101 (404).
- Edit: line 111 (assigned developer updates status), line 124 (developer can't set Accepted Risk), line 137
  (analyst can), line 149 (extra form fields like `AssigneeId` are ignored).
- Assign: line 168 (analyst assigns with due date), line 184 (unknown user rejected), line 196 (developer can't reassign).

Posting a form in a test needs the antiforgery token, so the helper `SubmitFormAsync` in `TestAppFactory.cs` loads
the form first and sends its token back, just like a browser.

## Try it yourself

1. Log in as `developer`, then log out (so the developer is in the user list).
2. Log in as `analyst`, click "New finding", create one.
3. On its page click "Assign", pick `developer` and a due date.
4. Log in as `developer`, open the finding, click "Edit": the status list has no AcceptedRisk.

## Questions you might get

**Why check access on POST and not only when showing the form?** An attacker doesn't need our form. They can send
the POST directly, for example with Burp Suite.

**What is the antiforgery token for?** CSRF protection. Another site can make the user's browser send a POST to our
app with the user's cookie, but it can't read our page to get the token, so the request is rejected.

**Why store users locally if Keycloak has them?** To list who can be assigned without giving the app admin access
to Keycloak. Downside: a user must log in once first.

**What happens with a severity value like 99?** `EnumDataType` makes validation fail, so the form is shown again and
nothing is saved.
